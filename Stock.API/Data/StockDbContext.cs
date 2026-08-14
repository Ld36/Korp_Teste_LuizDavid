using Microsoft.EntityFrameworkCore;
using Stock.API.Models;

namespace Stock.API.Data;

public class StockDbContext : DbContext
{
    public StockDbContext(DbContextOptions<StockDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Garante que o código do produto seja unico
        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Code)
            .IsUnique();

        // Configura o xmin do PostgreSQL como token de concorrencia
        modelBuilder.Entity<Product>()
            .Property(p => p.RowVersion)
            .IsRowVersion();
    }
}