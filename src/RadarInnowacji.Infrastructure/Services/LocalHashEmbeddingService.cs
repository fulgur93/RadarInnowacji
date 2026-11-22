using RadarInnowacji.Core.Interfaces;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>
/// Lokalny embedding cechowy (feature hashing) bez modeli i bez połączeń sieciowych.
/// Działa na CPU, jest deterministyczny i nie wymaga Ollamy.
/// </summary>
public sealed class LocalHashEmbeddingService : IEmbeddingService
{
    public const string ProviderId = "Lokalny";
    public string ProviderName => $"{ProviderId} (hashed features, v1)";

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var vector = new float[TextFeatures.EmbeddingDimensions];
        foreach (var (feature, weight) in TextFeatures.Features(text))
        {
            var index = (int)(StableHash(feature) % (uint)vector.Length);
            // Podwójny kierunek skrótu ogranicza liczbę kolizji przy małej liczbie wymiarów.
            var sign = (StableHash("s:" + feature) & 1) == 0 ? 1.0 : -1.0;
            vector[index] += (float)(Math.Sqrt(weight) * sign);
        }

        return Task.FromResult(EmbeddingMath.Normalize(vector));
    }

    private static uint StableHash(string value)
    {
        uint hash = 2166136261;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= 16777619;
        }

        return hash;
    }
}
