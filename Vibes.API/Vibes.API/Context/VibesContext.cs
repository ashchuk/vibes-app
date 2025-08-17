using Microsoft.EntityFrameworkCore;
using Vibes.API.Models;

namespace Vibes.API.Context;

public class VibesContext : DbContext, IVibesContext
{
    public DbSet<VibesUser> VibesUsers { get; set; }
    public DbSet<VibesMetric> VibesMetrics { get; set; }
    public DbSet<DailyPlan> DailyPlans { get; set; }
    public DbSet<EventRating> EventRatings { get; set; }
    
    Task IVibesContext.SaveChangesAsync() => SaveChangesAsync();
    Task IVibesContext.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    public VibesContext(DbContextOptions<VibesContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        _ = modelBuilder.Entity<VibesUser>()
            .ToTable("VibesUser")
            .Property(p => p.Id)
            .ValueGeneratedOnAdd();
        
        _ = modelBuilder.Entity<VibesUser>() // Так мы будем хранить enum как строку в БД
            .Property(e => e.State)
            .HasConversion<string>(); 
        
        _ = modelBuilder.Entity<EventRating>() // Так мы будем хранить enum как строку в БД
            .Property(e => e.Vibe)
            .HasConversion<string>();
        
        _ = modelBuilder.Entity<VibesMetric>()
            .ToTable("VibesMetrics")
            .Property(p => p.Id)
            .ValueGeneratedOnAdd();
    }

    public async Task<T> AddNewRecord<T>(T newItem) where T : class
    {
        DbSet<T> dbSet = Set<T>();
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> result = dbSet.Add(newItem);
        _ = await SaveChangesAsync();
        return result.Entity;
    }

    public async Task<T> UpdateRecord<T>(T record) where T : class
    {
        DbSet<T> dbSet = Set<T>();
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> result = dbSet.Update(record);
        _ = await SaveChangesAsync();
        return result.Entity;
    }

    public async Task<T> RemoveRecord<T>(T record) where T : class
    {
        DbSet<T> dbSet = Set<T>();
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> result = dbSet.Remove(record);
        _ = await SaveChangesAsync();
        return result.Entity;
    }

    public async Task<T> AddNewRecord<T>(T newItem, CancellationToken cancellationToken) where T : class
    {
        DbSet<T> dbSet = Set<T>();
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> result = dbSet.Add(newItem);
        _ = await SaveChangesAsync(cancellationToken);
        return result.Entity;
    }

    public async Task<T> UpdateRecord<T>(T record, CancellationToken cancellationToken) where T : class
    {
        DbSet<T> dbSet = Set<T>();
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> result = dbSet.Update(record);
        _ = await SaveChangesAsync(cancellationToken);
        return result.Entity;
    }

    public async Task<T> RemoveRecord<T>(T record, CancellationToken cancellationToken) where T : class
    {
        DbSet<T> dbSet = Set<T>();
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> result = dbSet.Remove(record);
        _ = await SaveChangesAsync(cancellationToken);
        return result.Entity;
    }

    public void ClearChangeTracker() => this.ChangeTracker.Clear();
}
