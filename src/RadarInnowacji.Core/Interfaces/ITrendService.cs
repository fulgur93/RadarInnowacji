using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

/// <summary>Trendy, wykresy aktywności i porównanie kategorii.</summary>
public interface ITrendService
{
    Task<TrendAnalysisModel> GetTrendsAsync(int months = 6, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryComparison>> GetCategoryComparisonAsync(
        int months = 6,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectActivityPoint>> GetActivityAsync(
        int days = 30,
        CancellationToken cancellationToken = default);
}
