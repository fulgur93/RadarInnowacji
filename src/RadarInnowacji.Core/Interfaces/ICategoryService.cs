using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryCount>> GetAllWithCountsAsync(CancellationToken cancellationToken = default);
}
