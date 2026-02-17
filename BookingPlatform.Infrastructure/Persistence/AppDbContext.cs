using BookingPlatform.Domain.Auth;
using BookingPlatform.Domain.Bookings;
using BookingPlatform.Domain.Buildings;
using BookingPlatform.Domain.Dictionaries;
using BookingPlatform.Domain.Equipment;
using BookingPlatform.Domain.Notifications;
using BookingPlatform.Domain.Rooms;
using BookingPlatform.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<RoomEquipment> RoomEquipment => Set<RoomEquipment>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingAuditLog> BookingAuditLogs => Set<BookingAuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserRoleEntry> UserRoles => Set<UserRoleEntry>();
    public DbSet<RoomTypeEntry> RoomTypes => Set<RoomTypeEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Справочники в отдельной схеме ref.
        modelBuilder.Entity<UserRoleEntry>(b =>
        {
            b.ToTable("UserRoles", "ref");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<RoomTypeEntry>(b =>
        {
            b.ToTable("RoomTypes", "ref");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Booking>(b =>
        {
            b.Property(x => x.StartTimeUtc).HasColumnName("StartUtc");
            b.Property(x => x.EndTimeUtc).HasColumnName("EndUtc");
        });

        modelBuilder.Entity<BookingAuditLog>(b =>
        {
            b.ToTable("BookingAuditLogs");
            b.HasKey(x => x.Id);
            b.Property(x => x.Action).HasMaxLength(128);
            b.Property(x => x.CorrelationId).HasMaxLength(128);
            b.Property(x => x.IpAddress).HasMaxLength(64);
            b.Property(x => x.UserAgent).HasMaxLength(512);
        });

        modelBuilder.Entity<Notification>(b =>
        {
            b.ToTable("Notifications");
            b.HasKey(x => x.Id);
            b.Property(x => x.Type).HasMaxLength(128);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
            b.Property(x => x.FailReason).HasMaxLength(1024);
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.CreatedAtUtc);
        });

        SeedDictionaries(modelBuilder);
    }

    private static void SeedDictionaries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRoleEntry>().HasData(
            new UserRoleEntry { Id = 1, Code = "SYSTEM", Name = "System" },
            new UserRoleEntry { Id = 2, Code = "ADMIN", Name = "Admin" },
            new UserRoleEntry { Id = 3, Code = "USER", Name = "User" },
            new UserRoleEntry { Id = 4, Code = "EMPLOYEE", Name = "Employee" }
        );

        modelBuilder.Entity<RoomTypeEntry>().HasData(
            new RoomTypeEntry { Id = 1, Code = "LECTURE", Name = "Lecture" },
            new RoomTypeEntry { Id = 2, Code = "LAB", Name = "Lab" },
            new RoomTypeEntry { Id = 3, Code = "SEMINAR", Name = "Seminar" },
            new RoomTypeEntry { Id = 4, Code = "MEETING", Name = "Meeting" },
            new RoomTypeEntry { Id = 5, Code = "OTHER", Name = "Other" }
        );
    }
}
