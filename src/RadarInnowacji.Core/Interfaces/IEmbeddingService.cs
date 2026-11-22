namespace RadarInnowacji.Core.Interfaces;

/// <summary>Generuje wektory (embeddingi) dla tekstu. Implementacje działają lokalnie.</summary>
public interface IEmbeddingService
{
    /// <summary>Nazwa providera zapisywana przy wektorach (widoczna także w interfejsie).</summary>
    string ProviderName { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
