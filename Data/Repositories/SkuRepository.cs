using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stack_Solver.Models;

namespace Stack_Solver.Data.Repositories
{
    public class SkuRepository(IDbContextFactory<ApplicationDbContext> factory, ILogger<SkuRepository> logger) : ISkuRepository
    {
        public event EventHandler<SKU>? SkuAdded;
        public event EventHandler<SKU>? SkuUpdated;
        public event EventHandler<string>? SkuDeleted;

        public async Task<IList<SKU>> GetAllAsync(CancellationToken ct = default)
        {
            using var db = await factory.CreateDbContextAsync(ct);
            return await db.Skus.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
        }

        public async Task<SKU?> GetAsync(string skuId, CancellationToken ct = default)
        {
            using var db = await factory.CreateDbContextAsync(ct);
            return await db.Skus.FindAsync([skuId], ct);
        }

        public async Task AddAsync(SKU sku, CancellationToken ct = default)
        {
            using var db = await factory.CreateDbContextAsync(ct);
            db.Skus.Add(sku);
            await db.SaveChangesAsync(ct);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("SKU added: {SkuId}", sku.SkuId);
            }
            SkuAdded?.Invoke(this, sku);
        }

        public async Task UpdateAsync(SKU sku, CancellationToken ct = default)
        {
            using var db = await factory.CreateDbContextAsync(ct);
            db.Skus.Update(sku);
            await db.SaveChangesAsync(ct);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("SKU updated: {SkuId}", sku.SkuId);
            }
            SkuUpdated?.Invoke(this, sku);
        }

        public async Task DeleteAsync(string skuId, CancellationToken ct = default)
        {
            using var db = await factory.CreateDbContextAsync(ct);
            var entity = await db.Skus.FindAsync([skuId], ct);
            if (entity != null)
            {
                db.Skus.Remove(entity);
                await db.SaveChangesAsync(ct);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("SKU deleted: {SkuId}", skuId);
                }
                SkuDeleted?.Invoke(this, skuId);
            }
            else
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Delete skipped for missing SKU: {SkuId}", skuId);
                }
            }
        }

        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            using var db = await factory.CreateDbContextAsync(ct);
            return await db.SaveChangesAsync(ct);
        }
    }
}