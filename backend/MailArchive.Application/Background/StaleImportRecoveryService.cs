using MailArchive.Application.Abstractions;
using MailArchive.Domain.Entities;
using MailArchive.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
namespace MailArchive.API.Background;



/// <summary>
/// O PARAKATO KODIKAS EINAI TOU CLAUDE!!!!! PROSOXH AN EXEI GRAPSEI PIPES
/// </summary>

public class StaleImportRecoveryService : IHostedService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    public StaleImportRecoveryService(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IMailArchiveDbContext>();
        var orphanedImports = await db.ImportBatches
        .Where(x =>
        x.Status == ImportBatchStatus.Queued ||
        x.Status == ImportBatchStatus.Running)
        .ToListAsync(cancellationToken);
        if (orphanedImports.Count == 0)
            return;
        foreach (var importBatch in orphanedImports)
        {
            importBatch.Status = ImportBatchStatus.Failed;
            importBatch.CompletedAt = DateTime.UtcNow;
            db.ImportErrors.Add(new ImportError
            {
                Id = Guid.NewGuid(),
                ImportBatchId = importBatch.Id,
                Message = "Import was interrupted because the API stopped while it was running.",
            CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}