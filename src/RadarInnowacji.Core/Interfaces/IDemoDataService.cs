namespace RadarInnowacji.Core.Interfaces;

public interface IDemoDataService
{
    Task<bool> HasDemoDataAsync(CancellationToken cancellationToken = default);
    Task<int> LoadAsync(CancellationToken cancellationToken = default);
    Task<int> ClearAsync(CancellationToken cancellationToken = default);
}
