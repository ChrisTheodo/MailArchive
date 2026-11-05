using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using MailArchive.API.Background;
using MailArchive.API.Middleware;
using MailArchive.API.Security;
using MailArchive.API.Storage;
using MailArchive.Application;
using MailArchive.Application.Abstractions;
using MailArchive.Application.Attachments;
using MailArchive.Application.Audit;
using MailArchive.Application.Auth;
using MailArchive.Application.Emails;
using MailArchive.Application.Imports;
using MailArchive.Application.Imports.Parsing;
using MailArchive.Application.Mailboxes;
using MailArchive.Application.Users;
using MailArchive.Persistence;
using MailArchive.Persistence.Seed;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCors = "Frontend";
const string SmartScheme = "Smart";
const string MicrosoftScheme = "Microsoft";

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDbContext<MailArchiveDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IMailArchiveDbContext>(provider =>
    provider.GetRequiredService<MailArchiveDbContext>());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IStoragePathResolver, StoragePathResolver>();
builder.Services.AddScoped<IAttachmentStorageService, AttachmentStorageService>();

builder.Services.Configure<MailArchiveSettings>(
    builder.Configuration.GetSection("MailArchive"));

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMailboxService, MailboxService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCors, policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var pstParserProvider =
    builder.Configuration["MailArchive:PstParserProvider"] ?? "XstReader";

if (string.Equals(pstParserProvider, "XstReader",
        StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IPstParser, XstReaderPstParser>();
}
else if (string.Equals(pstParserProvider, "Mock",
        StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IPstParser, MockPstParser>();
}
else
{
    throw new InvalidOperationException(
        $"Unsupported PST parser provider '{pstParserProvider}'. Valid values are: XstReader, Mock.");
}

builder.Services.AddScoped<IPstImportProcessor, PstImportProcessor>();
builder.Services.AddScoped<IImportService, ImportService>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

// JWT configuration
var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "Jwt:Key is missing from configuration.");
}

// Microsoft configuration
var tenantId = builder.Configuration["AzureAd:TenantId"];
var clientId = builder.Configuration["AzureAd:ClientId"];
var clientSecret = builder.Configuration["AzureAd:ClientSecret"];

if (string.IsNullOrWhiteSpace(tenantId))
    throw new InvalidOperationException("AzureAd:TenantId is missing.");

if (string.IsNullOrWhiteSpace(clientId))
    throw new InvalidOperationException("AzureAd:ClientId is missing.");

if (string.IsNullOrWhiteSpace(clientSecret))
    throw new InvalidOperationException("AzureAd:ClientSecret is missing.");

// Authentication
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = SmartScheme;
        options.DefaultAuthenticateScheme = SmartScheme;

        // API requests without a valid cookie or JWT receive 401.
        // Microsoft login is triggered explicitly by the login endpoint.
        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddPolicyScheme(SmartScheme, SmartScheme, options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authorization =
                context.Request.Headers.Authorization.ToString();

            if (authorization.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase))
            {
                return JwtBearerDefaults.AuthenticationScheme;
            }

            return CookieAuthenticationDefaults.AuthenticationScheme;
        };
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "MailArchive.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.Path = "/";

        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],

            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    })
    .AddOpenIdConnect(MicrosoftScheme, options =>
    {
        options.Authority =
            $"https://login.microsoftonline.com/{tenantId}/v2.0";

        options.ClientId = clientId;
        options.ClientSecret = clientSecret;

        options.SignInScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        options.CallbackPath =
            builder.Configuration["AzureAd:CallbackPath"] ?? "/signin-oidc";

        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = false;
        options.MapInboundClaims = false;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer =
                $"https://login.microsoftonline.com/{tenantId}/v2.0"
        };

        options.Events.OnTokenValidated = async context =>
        {
            var principal = context.Principal;

            var microsoftTenantId =
                principal?.FindFirst("tid")?.Value;

            var microsoftObjectId =
                principal?.FindFirst("oid")?.Value;

            // Validate that the account belongs to our company tenant.
            if (!string.Equals(
                    microsoftTenantId,
                    tenantId,
                    StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(microsoftObjectId))
            {
                context.Fail("Invalid Microsoft tenant or user identity.");
                return;
            }

            var db = context.HttpContext.RequestServices
                .GetRequiredService<MailArchiveDbContext>();

            // Only users already registered in our database are allowed.
            var user = await db.Users.SingleOrDefaultAsync(
                x => x.MicrosoftTenantId == microsoftTenantId &&
                     x.MicrosoftObjectId == microsoftObjectId &&
                     x.IsActive);

            if (user == null)
            {
                context.Fail(
                    "This Microsoft account is not registered in MailArchive.");
                return;
            }

            var identity = principal?.Identity as ClaimsIdentity;

            if (identity == null)
            {
                context.Fail("Could not create the application identity.");
                return;
            }

            // Replace Microsoft identity claims with local MailArchive claims.
            foreach (var claim in identity
                         .FindAll(ClaimTypes.NameIdentifier).ToList())
            {
                identity.RemoveClaim(claim);
            }

            foreach (var claim in identity
                         .FindAll(ClaimTypes.Email).ToList())
            {
                identity.RemoveClaim(claim);
            }

            foreach (var claim in identity
                         .FindAll(ClaimTypes.Name).ToList())
            {
                identity.RemoveClaim(claim);
            }

            foreach (var claim in identity
                         .FindAll(ClaimTypes.Role).ToList())
            {
                identity.RemoveClaim(claim);
            }

            identity.AddClaim(new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()));

            identity.AddClaim(new Claim(
                ClaimTypes.Email,
                user.Email));

            identity.AddClaim(new Claim(
                ClaimTypes.Name,
                user.DisplayName));

            identity.AddClaim(new Claim(
                ClaimTypes.Role,
                user.Role.ToString()));
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    const string bearerScheme = "Bearer";

    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MailArchive API",
        Version = "v1"
    });

    options.AddSecurityDefinition(bearerScheme, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Description = "Paste only the JWT access token. Do not include the word Bearer."
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference(bearerScheme, document),
                new List<string>()
            }
        });
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<QueuedHostedService>();
builder.Services.AddHostedService<StaleImportRecoveryService>();

var app = builder.Build();

if (await TestEmailSeederCommand.TryRunAsync(
        app.Services, args, app.Environment.ContentRootPath))
{
    return;
}

app.UseMiddleware<ExceptionMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MailArchiveDbContext>();

    await db.Database.MigrateAsync();
    await DataSeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors(FrontendCors);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();