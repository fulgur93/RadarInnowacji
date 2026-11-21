using Microsoft.Extensions.Logging;

namespace RadarInnowacji.Infrastructure.Logging;

/// <summary>
/// Minimalny dostawca logowania do pliku. Nie wymaga dodatkowych bibliotek
/// i nie może zatrzymać aplikacji w razie problemu z zapisem logu.
/// </summary>
internal sealed class FileLoggerProvider(string logDirectory, LogLevel minimumLevel) : ILoggerProvider
{
    private readonly object _gate = new();
    private StreamWriter? _writer;

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName, minimumLevel);

    internal void Write(string line)
    {
        lock (_gate)
        {
            try
            {
                _writer ??= new StreamWriter(GetLogPath(), append: true) { AutoFlush = true };
                _writer.WriteLine(line);
            }
            catch (IOException)
            {
                // Logowanie nie może przerywać działania aplikacji.
            }
            catch (UnauthorizedAccessException)
            {
                // Logowanie nie może przerywać działania aplikacji.
            }
        }
    }

    private string GetLogPath()
    {
        Directory.CreateDirectory(logDirectory);
        return Path.Combine(logDirectory, $"radar-{DateTime.Now:yyyyMMdd}.log");
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}

internal sealed class FileLogger(
    FileLoggerProvider provider,
    string categoryName,
    LogLevel minimumLevel) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= minimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel,-11}] {categoryName} [{eventId.Id}] {message}";

        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        provider.Write(line);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
