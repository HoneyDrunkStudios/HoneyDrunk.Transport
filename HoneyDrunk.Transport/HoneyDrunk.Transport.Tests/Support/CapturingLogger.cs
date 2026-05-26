using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace HoneyDrunk.Transport.Tests.Support;

/// <summary>
/// Logger that reports every level as enabled (so `if (logger.IsEnabled(LogLevel.Debug))`
/// guarded blocks actually run during tests) and records each entry for assertion.
/// Production code paths that emit structured logs only when a level is enabled go
/// uncovered with the default <c>NullLogger</c>.
/// </summary>
/// <typeparam name="T">The category type used by the logger.</typeparam>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly ConcurrentBag<(LogLevel level, string message)> _entries = new();

    /// <summary>Gets the recorded log entries.</summary>
    public IReadOnlyCollection<(LogLevel level, string message)> Entries => [.. _entries];

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _entries.Add((logLevel, formatter(state, exception)));
    }
}
