using MassTransit;
using Microsoft.EntityFrameworkCore;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Infrastructure.Persistence;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(320);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Name).HasMaxLength(256);
            entity.Property(u => u.Image).HasMaxLength(2048);
            entity.Property(u => u.CreatedAt).IsRequired();
            entity.Ignore(u => u.PasswordHash);
            entity.HasMany(u => u.Accounts).WithOne().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(a => a.Id);
            // Ids are created in the domain. Without this, EF treats a new
            // account added to a loaded user's collection as an existing row
            // (because its key is already set) and issues an UPDATE.
            entity.Property(a => a.Id).ValueGeneratedNever();
            entity.Property(a => a.ProviderId).IsRequired().HasMaxLength(64);
            entity.Property(a => a.ProviderAccountId).IsRequired().HasMaxLength(256);
            entity.HasIndex(a => new { a.ProviderId, a.ProviderAccountId }).IsUnique();
            entity.Property(a => a.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.ToTable("sessions");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.TokenHash).IsRequired().HasMaxLength(64);
            entity.HasIndex(s => s.TokenHash).IsUnique();
            entity.Property(s => s.ExpiresAt).IsRequired();
            entity.Property(s => s.CreatedAt).IsRequired();
            entity.Property(s => s.IpAddress).HasMaxLength(64);
            entity.Property(s => s.UserAgent).HasMaxLength(512);
            entity.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // MassTransit transactional outbox: published events are saved here in
        // the same transaction as the entities, then delivered to the broker.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
