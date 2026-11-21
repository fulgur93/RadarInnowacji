using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Infrastructure.Logging;

public static class RadarLoggingExtensions
{
    public static ILoggingBuilder AddRadarFileLogging(this ILoggingBuilder builder, IConfiguration configuration)
    {
        var section = configuration.GetSection("Logging:File");
        var configuredDirectory = section.GetValue<string>("Directory");
        var directory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? RadarDataPaths.LogDirectory
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredDirectory));
        var minimumLevel = section.GetValue("MinimumLevel", LogLevel.Information);

        Directory.CreateDirectory(directory);
        builder.AddProvider(new FileLoggerProvider(directory, minimumLevel));

        return builder;
    }
}
