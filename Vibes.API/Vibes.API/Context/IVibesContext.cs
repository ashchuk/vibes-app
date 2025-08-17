using Microsoft.EntityFrameworkCore;
using Vibes.API.Models;

namespace Vibes.API.Context;

public interface IVibesContext
{
    public DbSet<VibesUser> VibesUsers { get; set; }
    public DbSet<VibesMetric> VibesMetrics { get; set; }
    public DbSet<DailyPlan> DailyPlans { get; set; }
    
    public Task SaveChangesAsync();
    public Task SaveChangesAsync(CancellationToken cancellationToken);

    public Task<T> AddNewRecord<T>(T newItem) where T : class;
    public Task<T> UpdateRecord<T>(T record) where T : class;
    public Task<T> RemoveRecord<T>(T record) where T : class;
    public Task<T> AddNewRecord<T>(T newItem, CancellationToken cancellationToken) where T : class;
    public Task<T> UpdateRecord<T>(T record, CancellationToken cancellationToken) where T : class;
    public Task<T> RemoveRecord<T>(T record, CancellationToken cancellationToken) where T : class;
    public void ClearChangeTracker();
}