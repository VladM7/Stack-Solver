using Microsoft.EntityFrameworkCore;
using Stack_Solver.Models;

namespace Stack_Solver.Data.Repositories
{
    public class SkuRepository(IDbContextFactory<ApplicationDbContext> factory) : ISkuRepository
    {
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
        }

        public async Task UpdateAsync(SKU sku, CancellationToken ct = default)
        {
            using var db = await factory.CreateDbContextAsync(ct);
            db.Skus.Update(sku);
            await db.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(string skuId, CancellationToken ct = default)
        {
            using var db = await factory.CreateDbContextAsync(ct);
            var entity = await db.Skus.FindAsync(new object?[] { skuId }, ct);
            if (entity != null)
            {
                db.Skus.Remove(entity);
                await db.SaveChangesAsync(ct);
            }
        }

        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            using var db = await factory.CreateDbContextAsync(ct);
            return await db.SaveChangesAsync(ct);
        }
    }
}