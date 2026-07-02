using Microsoft.EntityFrameworkCore;
using Stack_Solver.Models;
using Stack_Solver.Models.Jobs;

namespace Stack_Solver.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<SKU> Skus => Set<SKU>();
        public DbSet<Job> Jobs => Set<Job>();

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

            modelBuilder.Entity<Job>(e =>
            {
                e.ToTable("Jobs");
                e.HasKey(j => j.Id);
                // Store the status as its name rather than an ordinal so rows stay readable and
                // survive any future reordering of the enum.
                e.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);
                e.HasIndex(j => j.CreatedAt);
            });
        }
    }
}