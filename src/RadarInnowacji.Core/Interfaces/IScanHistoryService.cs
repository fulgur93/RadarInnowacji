using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

public interface IScanHistoryService
{
    Task<IReadOnlyList<ScanHistoryItem>> GetRecentAsync(int count = 50, CancellationToken cancellationToken = default);
}
