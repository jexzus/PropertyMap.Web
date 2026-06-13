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

        // List<string> Fotos se guarda como JSON en una columna nvarchar
        builder.Entity<PropertyListing>()
            .Property(p => p.Fotos)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
            )
            .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),
                v => v.Aggregate(0, (a, s) => HashCode.Combine(a, s.GetHashCode())),
                v => v.ToList()
            ));
    }
}
