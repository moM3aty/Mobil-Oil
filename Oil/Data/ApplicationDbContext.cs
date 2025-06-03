using Microsoft.EntityFrameworkCore;
using Oil.Models;

namespace Oil.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<ProductCategory> ProductCategories { get; set; }
        public DbSet<ProductType> ProductTypes { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<ShippingZone> ShippingZones { get; set; }
        public DbSet<ShippingCost> ShippingCosts { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderItem>()
                .Property(o => o.Price)
                .HasPrecision(18, 2);
            modelBuilder.Entity<ShippingZone>()
             .HasIndex(sz => sz.NameAr)
            .IsUnique();
            modelBuilder.Entity<ShippingZone>()
            .HasIndex(sz => sz.NameEn)
            .IsUnique();
        }



    }
}
