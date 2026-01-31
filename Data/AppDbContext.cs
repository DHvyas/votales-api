using Microsoft.EntityFrameworkCore;
using VoTales.API.Models;

namespace VoTales.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tale> Tales => Set<Tale>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Id is NOT auto-generated - it comes from Supabase auth.users(id)
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Bio).HasMaxLength(300);
            entity.Property(e => e.AvatarStyle).HasMaxLength(50).HasDefaultValue("initials");
            entity.HasIndex(e => e.DisplayName);
        });

        modelBuilder.Entity<Tale>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.AuthorId).IsRequired();
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserEmail).IsRequired();
            entity.Property(e => e.Message).IsRequired();
        });

        modelBuilder.Entity<Vote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.TaleId).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.TaleId }).IsUnique();
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.TriggeredById).IsRequired();
            entity.Property(e => e.TriggeredByName).IsRequired();
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.Message).IsRequired();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsRead });
        });
    }
}
