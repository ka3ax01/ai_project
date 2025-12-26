using BookingPlatform.Domain.Bookings;
using BookingPlatform.Domain.Buildings;
using BookingPlatform.Domain.Equipment;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Здесь позже добавим конфигурации сущностей
    }
}

