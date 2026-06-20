using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.Entities;
using System.Text.Json;

namespace PropertyMap.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Location> Locations { get; set; }
    public DbSet<Publisher> Publishers { get; set; }
    public DbSet<PropertyListing> PropertyListings { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PropertyListing>()
            .Property(p => p.Precio)
            .HasPrecision(18, 2);

        builder.Entity<PropertyListing>()
            .Property(p => p.Superficie)
            .HasPrecision(10, 2);

        builder.Entity<PropertyListing>()
            .Property(p => p.SuperficieCubierta)
            .HasPrecision(10, 2);

        var stringListComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            v => v.Aggregate(0, (a, s) => HashCode.Combine(a, s.GetHashCode())),
            v => v.ToList());

        // List<string> Amenities se guarda como JSON en una columna nvarchar
        builder.Entity<PropertyListing>()
            .Property(p => p.Amenities)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? new List<string>() : (JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            )
            .Metadata.SetValueComparer(stringListComparer);
    }
}
