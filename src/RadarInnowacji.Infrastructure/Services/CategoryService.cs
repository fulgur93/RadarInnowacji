using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Infrastructure.Services;

public sealed class CategoryService(RadarDbContext db) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryCount>> GetAllWithCountsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Categories
            .Where(x => x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Name,
                Count = db.ProjectCategories.Count(p => p.CategoryId == x.Id)
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new CategoryCount(x.Id, x.Name, x.Count))
            .ToList();
    }
}
