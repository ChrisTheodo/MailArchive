using MailArchive.Application.Abstractions;
using MailArchive.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Persistence;

public class MailArchiveDbContext : DbContext, IMailArchiveDbContext
{
    public MailArchiveDbContext(DbContextOptions<MailArchiveDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Mailbox> Mailboxes => Set<Mailbox>();

    public DbSet<Email> Emails => Set<Email>();

    public DbSet<EmailRecipient> EmailRecipients => Set<EmailRecipient>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<ImportError> ImportErrors => Set<ImportError>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MailArchiveDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        RemoveNulCharacters();

        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        RemoveNulCharacters();

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RemoveNulCharacters();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>
    /// Η PostgreSQL δεν δέχεται τον χαρακτήρα NUL (0x00) σε στήλες κειμένου.
    /// Δίχτυ ασφαλείας: καθαρίζει όλα τα string πριν την αποθήκευση.
    /// </summary>
    private void RemoveNulCharacters()
    {
        foreach (var entry in ChangeTracker.Entries()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            foreach (var property in entry.Properties)
            {
                if (property.CurrentValue is string text && text.Contains('\0'))
                {
                    property.CurrentValue = text.Replace("\0", string.Empty);
                }
            }
        }
    }
}