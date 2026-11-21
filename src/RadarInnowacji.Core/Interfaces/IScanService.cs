using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

public interface IScanService
{
    Task<ScanResult> ScanSourceAsync(int sourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScanResult>> ScanDueSourcesAsync(CancellationToken cancellationToken = default);
}
