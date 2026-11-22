using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

public class EmbeddingTests
{
    [Fact]
    public async Task LokalnyEmbedding_JestDeterministycznyIZnormalizowany()
    {
        IEmbeddingService service = new LocalHashEmbeddingService();
        const string text = "Inteligentna stacja pomiaru jakości powietrza z czujnikami";

        var first = await service.EmbedAsync(text);
        var second = await service.EmbedAsync(text);

        Assert.Equal(first, second);
        Assert.Equal(TextFeatures.EmbeddingDimensions, first.Length);
        Assert.InRange(Math.Sqrt(first.Sum(x => (double)x * x)), 0.99, 1.01);
    }

    [Fact]
    public async Task LokalnyEmbedding_PodobneTematyMajaWyzszePodobienstwoNizRozne()
    {
        IEmbeddingService service = new LocalHashEmbeddingService();
        var airQuality = await service.EmbedAsync(
            "Inteligentna stacja pomiaru jakości powietrza. Czujniki zanieczyszczeń monitorują powietrze w szkołach.");
        var airSensors = await service.EmbedAsync(
            "Niskokosztowe czujniki zanieczyszczeń powietrza. System monitoringu jakości powietrza w klasach.");
        var unrelated = await service.EmbedAsync(
            "Egzoszkielet rehabilitacyjny wspomaga ruch kończyn dolnych pacjentów.");

        var similar = EmbeddingMath.CosineSimilarity(airQuality, airSensors);
        var different = EmbeddingMath.CosineSimilarity(airQuality, unrelated);

        Assert.True(similar > different + 0.15, $"Podobieństwo tematów: {similar:0.000} vs {different:0.000}.");
    }

    [Fact]
    public async Task OllamaEmbedding_ParsujeWektorIZadanieZawieraModel()
    {
        string? requestBody = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Equal("/api/embed", request.RequestUri!.AbsolutePath);
            return FakeHttpMessageHandler.Json("{\"embeddings\":[[0.5,0.5]]}");
        });
        var options = Options.Create(new RadarOptions
        {
            Categories = new CategoryOptions { OllamaEmbeddingModel = "multilingual-e5-small" }
        });
        var service = new OllamaEmbeddingService(new FakeEmbeddingHttpClientFactory(new HttpClient(handler)), options);

        var vector = await service.EmbedAsync("jakość powietrza");

        Assert.Equal(2, vector.Length);
        Assert.Contains("multilingual-e5-small", requestBody);
        Assert.InRange(Math.Sqrt(vector.Sum(x => (double)x * x)), 0.99, 1.01);
    }

    [Fact]
    public async Task OllamaEmbedding_BladHttpZwracaCzytelnyKomunikat()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("model not found")
        });
        var service = new OllamaEmbeddingService(
            new FakeEmbeddingHttpClientFactory(new HttpClient(handler)),
            Options.Create(new RadarOptions()));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.EmbedAsync("test"));

        Assert.Contains("404", exception.Message);
        Assert.Contains("model not found", exception.Message);
    }

    private sealed class FakeEmbeddingHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
