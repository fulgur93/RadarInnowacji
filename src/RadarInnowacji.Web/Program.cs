using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Infrastructure;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Logging;
using RadarInnowacji.Web.Components;
using RadarInnowacji.Web.Services;
using RadarInnowacji.Worker.Jobs;

// Skrót na pulpicie może uruchomić program z innym katalogiem roboczym – wtedy
// appsettings.json i wwwroot muszą być szukane w katalogu instalacji.
if (!File.Exists(Path.Combine(Environment.CurrentDirectory, "appsettings.json")) &&
    File.Exists(Path.Combine(AppContext.BaseDirectory, "appsettings.json")))
{
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);
}

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddRadarFileLogging(builder.Configuration);

var radarOptions = RadarOptions.Load(builder.Configuration);
RadarDataPaths.EnsureDirectories();

var instanceFile = new RadarInstanceFile(Path.Combine(RadarDataPaths.AppDataDirectory, "radar.url"));
SingleInstanceGuard? singleInstanceGuard = null;
Mutex? installerMutex = null;
if (radarOptions.SingleInstance)
{
    var lockPath = Path.Combine(RadarDataPaths.AppDataDirectory, "radar.lock");
    singleInstanceGuard = new SingleInstanceGuard(lockPath);
    if (!singleInstanceGuard.TryAcquire())
    {
        // Nie zwalniamy cudzej blokady (Dispose usuwa plik) – po prostu pokazujemy działającą instancję.
        var runningUrl = instanceFile.TryRead();
        Console.WriteLine(runningUrl is null
            ? "Radar Innowacji jest już uruchomiony."
            : $"Radar Innowacji jest już uruchomiony pod adresem {runningUrl}.");
        if (runningUrl is not null && radarOptions.OpenBrowser)
        {
            BrowserLauncher.TryOpen(runningUrl, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        }

        return;
    }

    // Nazwany mutex pozwala instalatorowi (Inno Setup, AppMutex) wykryć działającą aplikację.
    if (OperatingSystem.IsWindows())
    {
        installerMutex = new Mutex(false, "RadarInnowacji_AppMutex");
    }
}

// Port 0 jest używany w testach integracyjnych – Kestrel wybiera wtedy wolny port.
var port = radarOptions.Port == 0 ? 0 : PortSelector.FindAvailable(radarOptions.Port);
var appUrl = $"http://127.0.0.1:{port}";
builder.WebHost.UseUrls(appUrl);
builder.Services.AddSingleton(new RadarRuntimeInfo(port == 0 ? string.Empty : appUrl));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRadarInfrastructure(builder.Configuration);
builder.Services.AddHostedService<DatabaseMigrationJob>();
builder.Services.AddHostedService<PermanentSourcesSeedJob>();
builder.Services.AddHostedService<BackgroundScanJob>();
builder.Services.AddHostedService<AiAnalysisJob>();
builder.Services.AddHostedService<CategoryAnalysisJob>();
builder.Services.AddHostedService<ProjectAnalysisJob>();
builder.Services.AddHostedService<AssessmentJob>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();

// Eksport projektów z oceną i rekomendacją – punkt integracji z innymi systemami.
// Aplikacja nasłuchuje wyłącznie na 127.0.0.1, więc eksport nie jest dostępny z sieci.
app.MapGet("/eksport/projekty.csv", async (IPipelineService pipeline, CancellationToken cancellationToken) =>
{
    var csv = await pipeline.ExportCsvAsync(cancellationToken);
    // BOM, aby arkusz kalkulacyjny poprawnie odczytał polskie znaki.
    var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray();
    return Results.File(bytes, "text/csv; charset=utf-8", $"radar-innowacji-projekty-{DateTime.Now:yyyy-MM-dd}.csv");
});
app.MapGet("/eksport/projekty.json", async (IPipelineService pipeline, CancellationToken cancellationToken) =>
    Results.Json(await pipeline.ExportItemsAsync(cancellationToken), new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    }));
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("RadarInnowacji.Startup");
startupLogger.LogInformation("Radar Innowacji nasłuchuje na {Url}", appUrl);

if (port != 0)
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        if (singleInstanceGuard is not null)
        {
            instanceFile.Write(appUrl);
        }

        Console.WriteLine();
        Console.WriteLine($"Radar Innowacji działa pod adresem {appUrl}");
        Console.WriteLine("Aby zakończyć: użyj przycisku „Zamknij aplikację” w menu albo zamknij to okno (Ctrl+C).");
        Console.WriteLine();
        if (radarOptions.OpenBrowser)
        {
            BrowserLauncher.TryOpen(appUrl, startupLogger);
        }
    });
}

if (OperatingSystem.IsWindows() && port != 0 && !Console.IsOutputRedirected)
{
    try
    {
        Console.Title = $"Radar Innowacji – {appUrl}";
    }
    catch (IOException)
    {
        // Brak okna konsoli – tytuł nie jest wymagany.
    }
}

try
{
    await app.RunAsync();
}
finally
{
    if (singleInstanceGuard is not null)
    {
        instanceFile.Delete();
        singleInstanceGuard.Dispose();
    }

    installerMutex?.Dispose();
}

public partial class Program;
