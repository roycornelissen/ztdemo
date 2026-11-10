using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Logging;
using Shouldly;

namespace PaymentsApiTests.Security;

public class SecurityEventTests
{
    [Test]
    public async Task Denials_are_correlatable_without_logging_tokens_or_raw_urls()
    {
        var logs = new RecordingLoggerProvider();
        using var factory = new PaymentsApiFactory(services => services.AddLogging(builder => builder.AddProvider(logs)));
        using var client = factory.CreateClient();
        var token = PaymentsApiFactory.CreateToken("Accounts.Read");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/test-endpoint?private=private-value");
        request.Headers.Add("traceparent", "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var entry = logs.Entries.Single(entry => entry.EventId.Id == 1000 && entry.Category == "MiniBank.Security");
        entry.Message.ShouldContain("403");
        entry.Message.ShouldContain("/test-endpoint");
        entry.Message.ShouldContain("0123456789abcdef0123456789abcdef");
        entry.Message.ShouldNotContain("private-value");
        entry.Message.ShouldNotContain(token);
        IdentityModelEventSource.ShowPII.ShouldBeFalse();
        IdentityModelEventSource.LogCompleteSecurityArtifact.ShouldBeFalse();
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Entries);
        public void Dispose() { }
    }

    private sealed record LogEntry(string Category, EventId EventId, string Message);

    private sealed class RecordingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(category, eventId, formatter(state, exception)));
    }
}
