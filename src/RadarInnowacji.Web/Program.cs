using RadarInnowacji.Infrastructure;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Logging;
using RadarInnowacji.Web.Components;
using RadarInnowacji.Web.Services;
using RadarInnowacji.Worker.Jobs;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddRadarFileLogging(builder.Configuration);

var radarOptions = RadarOptions.Load(builder.Configuration);
RadarDataPaths.EnsureDirectories();

SingleInstanceGuard? singleInstanceGuard = null;
if (radarOptions.SingleInstance)
{
    var lockPath = Path.Combine(RadarDataPaths.AppDataDirectory, "radar.lock");
    singleInstanceGuard = new SingleInstanceGuard(lockPath);
    if (!singleInstanceGuard.TryAcquire())
    {
        singleInstanceGuard.Dispose();
        Console.WriteLine("Radar Innowacji jest już uruchomiony. Zamknij istniejącą instancję przed ponownym uruchomieniem.");
        return;
    }
}

// Port 0 jest używany w testach integracyjnych – Kestrel wybiera wtedy wolny port.
var port = radarOptions.Port == 0 ? 0 : PortSelector.FindAvailable(radarOptions.Port);
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRadarInfrastructure(builder.Configuration);
builder.Services.AddHostedService<DatabaseMigrationJob>();
builder.Services.AddHostedService<PermanentSourcesSeedJob>();
builder.Services.AddHostedService<BackgroundScanJob>();
builder.Services.AddHostedService<AiAnalysisJob>();
builder.Services.AddHostedService<CategoryAnalysisJob>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("RadarInnowacji.Startup");
startupLogger.LogInformation("Radar Innowacji nasłuchuje na http://127.0.0.1:{Port}", port);

if (radarOptions.OpenBrowser && port != 0)
{
    app.Lifetime.ApplicationStarted.Register(() =>
        BrowserLauncher.TryOpen($"http://127.0.0.1:{port}", startupLogger));
}

await app.RunAsync();

singleInstanceGuard?.Dispose();

public partial class Program;
