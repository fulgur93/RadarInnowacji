using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

public class AiAnalysisServiceTests
{
    [Fact]
    public async Task Ollama_ParsujeUstrukturyzowanyWynikIZadanieZawieraModel()
    {
        const string modelAnswer = "{\"isPotentialInnovation\":true,\"confidence\":85,\"innovationType\":\"Technologiczna\"," +
                                   "\"problem\":\"Zanieczyszczenie powietrza\",\"solution\":\"Nowy czujnik\"," +
                                   "\"noveltyDescription\":\"Autorski system\"," +
                                   "\"technologies\":[\"IoT\",\"AI\"],\"reason\":\"Opisuje nowe rozwiązanie\"," +
                                   "\"suggestedCategories\":[\"Robotyka\"],\"missingInformation\":\"Brak danych o kosztach\"}";
        var response = JsonSerializer.Serialize(new { message = new { content = modelAnswer } });
        string? requestBody = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return FakeHttpMessageHandler.Json(response);
        });

        var service = CreateService(handler, "qwen3:1.7b");
        var result = await service.AnalyzeAsync(CreateDiscovery());

        Assert.True(result.IsPotentialInnovation);
        Assert.Equal(85, result.Confidence);
        Assert.Equal("Technologiczna", result.InnovationType);
        Assert.Equal(new[] { "IoT", "AI" }, result.Technologies);
        Assert.Equal("Robotyka", Assert.Single(result.SuggestedCategories));
        Assert.Equal("Brak danych o kosztach", result.MissingInformation);
        Assert.Contains("qwen3:1.7b", requestBody);
        Assert.Contains("\"format\":\"json\"", requestBody);
    }

    [Fact]
    public async Task Ollama_ZwracaBlednyJsonJakoCzytelnyBlad()
    {
        var response = JsonSerializer.Serialize(new { message = new { content = "to nie jest json" } });
        var service = CreateService(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(response)), "model");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnalyzeAsync(CreateDiscovery()));

        Assert.Contains("JSON", exception.Message);
    }

    [Fact]
    public async Task Ollama_ZwracaBladHttpZKomunikatem()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("model crash")
        });
        var service = CreateService(handler, "model");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnalyzeAsync(CreateDiscovery()));

        Assert.Contains("500", exception.Message);
        Assert.Contains("model crash", exception.Message);
    }

    [Fact]
    public async Task Ollama_TestPolaczeniaWykrywaBrakModelu()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(
            "{\"models\":[{\"name\":\"qwen2.5-coder:latest\"}]}"));
        var service = CreateService(handler, "qwen3:1.7b");

        var result = await service.TestConnectionAsync();

        Assert.False(result.IsAvailable);
        Assert.Contains("qwen2.5-coder", result.Message);
        Assert.Equal("qwen3:1.7b", result.Model);
    }

    private static OllamaAiAnalysisService CreateService(HttpMessageHandler handler, string model)
    {
        var options = Options.Create(new RadarOptions
        {
            Ai = new AiOptions { Model = model }
        });
        return new OllamaAiAnalysisService(
            new FakeHttpClientFactory(new HttpClient(handler)),
            options,
            NullLogger<OllamaAiAnalysisService>.Instance);
    }

    private static Discovery CreateDiscovery() => new()
    {
        Title = "Nowy prototyp czujnika",
        Description = "Studenci zbudowali czujnik powietrza.",
        RawContent = "Szczegóły projektu.",
        SourceUrl = "https://example.org/project/1",
        PublishedAt = DateTime.UtcNow.AddDays(-1)
    };

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
