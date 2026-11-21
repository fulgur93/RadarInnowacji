using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
