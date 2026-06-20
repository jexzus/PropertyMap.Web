using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;

namespace PropertyMap.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<PropertyListing> PropertyListings => Set<PropertyListing>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<PropertyView> PropertyViews => Set<PropertyView>();
    public DbSet<PropertyFavorite> PropertyFavorites => Set<PropertyFavorite>();
    public DbSet<PropertyRating> PropertyRatings => Set<PropertyRating>();
    public DbSet<AgentRating> AgentRatings => Set<AgentRating>();
    public DbSet<PropertyQuestion> PropertyQuestions => Set<PropertyQuestion>();
    public DbSet<PropertyAnswer> PropertyAnswers => Set<PropertyAnswer>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PropertyListing decimal precision
        modelBuilder.Entity<PropertyListing>()
            .Property(p => p.Precio).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<PropertyListing>()
            .Property(p => p.Superficie).HasColumnType("decimal(10,2)");
        modelBuilder.Entity<PropertyListing>()
            .Property(p => p.SuperficieCubierta).HasColumnType("decimal(10,2)");

        // Amenities JSON serialization
        var listComparer = new ValueComparer<List<string>>(
            (c1, c2) => c1!.SequenceEqual(c2!),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        modelBuilder.Entity<PropertyListing>()
            .Property(p => p.Amenities)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(listComparer);

        // Publisher → ApplicationUser (1:1, optional)
        modelBuilder.Entity<Publisher>()
            .HasOne(p => p.User)
            .WithOne(u => u.Publisher)
            .HasForeignKey<Publisher>(p => p.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Unique constraints
        modelBuilder.Entity<PropertyFavorite>()
            .HasIndex(f => new { f.UserId, f.PropertyListingId }).IsUnique();
        modelBuilder.Entity<PropertyRating>()
            .HasIndex(r => new { r.UserId, r.PropertyListingId }).IsUnique();
        modelBuilder.Entity<AgentRating>()
            .HasIndex(r => new { r.UserId, r.PublisherId }).IsUnique();
        modelBuilder.Entity<NotificationPreference>()
            .HasIndex(np => np.UserId).IsUnique();
        modelBuilder.Entity<Subscription>()
            .HasIndex(s => s.UserId).IsUnique();

        // Plan
        modelBuilder.Entity<Plan>()
            .Property(p => p.PrecioMensual).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Plan>()
            .HasIndex(p => p.Slug).IsUnique();

        // PropertyView — optional FK (anonymous views)
        modelBuilder.Entity<PropertyView>()
            .HasOne(v => v.User)
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // AgentRating — avoid multiple cascade paths
        modelBuilder.Entity<AgentRating>()
            .HasOne(r => r.Publisher)
            .WithMany(p => p.Ratings)
            .HasForeignKey(r => r.PublisherId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AgentRating>()
            .HasOne(r => r.User)
            .WithMany(u => u.AgentRatings)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // PropertyRating — avoid multiple cascade paths
        modelBuilder.Entity<PropertyRating>()
            .HasOne(r => r.User)
            .WithMany(u => u.PropertyRatings)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // PropertyQuestion — avoid multiple cascade paths
        modelBuilder.Entity<PropertyQuestion>()
            .HasOne(q => q.User)
            .WithMany(u => u.Questions)
            .HasForeignKey(q => q.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Report — avoid multiple cascade paths
        modelBuilder.Entity<Report>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // PropertyFavorite cascade
        modelBuilder.Entity<PropertyFavorite>()
            .HasOne(f => f.User)
            .WithMany(u => u.Favorites)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Notification cascade
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Alert cascade
        modelBuilder.Entity<Alert>()
            .HasOne(a => a.User)
            .WithMany(u => u.Alerts)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // NotificationPreference 1:1
        modelBuilder.Entity<NotificationPreference>()
            .HasOne(np => np.User)
            .WithOne(u => u.NotificationPreference)
            .HasForeignKey<NotificationPreference>(np => np.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Subscription 1:1
        modelBuilder.Entity<Subscription>()
            .HasOne(s => s.User)
            .WithOne(u => u.Subscription)
            .HasForeignKey<Subscription>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // PropertyAnswer → Publisher — avoid multiple cascade paths
        modelBuilder.Entity<PropertyAnswer>()
            .HasOne(a => a.Publisher)
            .WithMany()
            .HasForeignKey(a => a.PublisherId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
