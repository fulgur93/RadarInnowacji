using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

public interface IDiscoveryService
{
    Task<DiscoveryPage> GetPageAsync(DiscoveryFilter filter, CancellationToken cancellationToken = default);
    Task<DiscoveryDetailsModel?> GetDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task MarkVerifiedAsync(int id, CancellationToken cancellationToken = default);
}
