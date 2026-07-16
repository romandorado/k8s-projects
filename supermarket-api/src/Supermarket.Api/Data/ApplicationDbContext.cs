using Microsoft.EntityFrameworkCore;
using Supermarket.Api.Models;

namespace Supermarket.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ShoppingItem> Items => Set<ShoppingItem>();
    public DbSet<ShoppingList> Lists => Set<ShoppingList>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ShoppingItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Price).HasPrecision(10, 2);
            entity.HasOne<ShoppingList>()
                .WithMany(l => l.Items)
                .HasForeignKey("ShoppingListId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShoppingList>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Budget).HasPrecision(10, 2);
        });
    }
}
