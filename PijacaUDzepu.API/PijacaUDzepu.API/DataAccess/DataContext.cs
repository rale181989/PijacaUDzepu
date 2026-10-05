using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.Models;

namespace PijacaUDzepu.API.DataAccess;

public class DataContext : IdentityDbContext<User, Role, int,
    IdentityUserClaim<int>, UserRole, IdentityUserLogin<int>,
    IdentityRoleClaim<int>, IdentityUserToken<int>>
{
    public DataContext(DbContextOptions<DataContext> options) : base(options) { }

    public DbSet<Market> Markets => Set<Market>();
    public DbSet<Stall> Stalls => Set<Stall>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorUser> VendorUsers => Set<VendorUser>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<VendorInvitation> VendorInvitations => Set<VendorInvitation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("unaccent");

        builder.Entity<UserRole>(entity =>
        {
            entity.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .IsRequired();

            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .IsRequired();
        });

        builder.Entity<VendorUser>(entity =>
        {
            entity.HasIndex(vu => new { vu.VendorId, vu.UserId }).IsUnique();

            entity.HasOne(vu => vu.Vendor)
                .WithMany(v => v.VendorUsers)
                .HasForeignKey(vu => vu.VendorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(vu => vu.User)
                .WithMany(u => u.VendorUsers)
                .HasForeignKey(vu => vu.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Product>(entity =>
        {
            entity.HasIndex(p => p.Name);
            entity.Property(p => p.Price).HasPrecision(10, 2);

            entity.HasOne(p => p.Vendor)
                .WithMany(v => v.Products)
                .HasForeignKey(p => p.VendorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Order>(entity =>
        {
            entity.Property(o => o.TotalAmount).HasPrecision(10, 2);
            entity.Ignore(o => o.IsGuestOrder);

            entity.HasOne(o => o.Customer)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            entity.HasOne(o => o.Vendor)
                .WithMany(v => v.Orders)
                .HasForeignKey(o => o.VendorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrderItem>(entity =>
        {
            entity.Property(oi => oi.UnitPrice).HasPrecision(10, 2);
            entity.Property(oi => oi.TotalPrice).HasPrecision(10, 2);
            entity.Property(oi => oi.Quantity).HasPrecision(10, 3);

            entity.HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Market>(entity =>
        {
            entity.HasIndex(m => m.Name);
        });

        builder.Entity<Stall>(entity =>
        {
            entity.HasIndex(s => new { s.MarketId, s.Label }).IsUnique();

            entity.HasOne(s => s.Market)
                .WithMany(m => m.Stalls)
                .HasForeignKey(s => s.MarketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<VendorInvitation>(entity =>
        {
            entity.HasIndex(vi => vi.Token).IsUnique();

            entity.HasOne(vi => vi.Vendor)
                .WithMany()
                .HasForeignKey(vi => vi.VendorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Vendor>(entity =>
        {
            entity.HasIndex(v => v.Name);

            entity.HasOne(v => v.Market)
                .WithMany(m => m.Vendors)
                .HasForeignKey(v => v.MarketId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.Stall)
                .WithMany(s => s.Vendors)
                .HasForeignKey(v => v.StallId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
