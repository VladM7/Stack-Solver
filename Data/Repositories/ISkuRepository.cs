using Stack_Solver.Models;

namespace Stack_Solver.Data.Repositories
{
    public interface ISkuRepository
    {
        Task<IList<SKU>> GetAllAsync(CancellationToken ct = default);
        Task<SKU?> GetAsync(string skuId, CancellationToken ct = default);
        Task AddAsync(SKU sku, CancellationToken ct = default);
        Task UpdateAsync(SKU sku, CancellationToken ct = default);
        Task DeleteAsync(string skuId, CancellationToken ct = default);
        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}