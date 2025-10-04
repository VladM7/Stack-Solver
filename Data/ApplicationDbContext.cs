using Microsoft.EntityFrameworkCore;
using Stack_Solver.Models;

namespace Stack_Solver.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<SKU> Skus => Set<SKU>();

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SKU>(e =>
            {
                e.ToTable("Skus");
                e.HasKey(s => s.SkuId);
                e.Property(s => s.Name).HasMaxLength(200);
                e.Property(s => s.Notes).HasMaxLength(1000);
            });
        }
    }
}