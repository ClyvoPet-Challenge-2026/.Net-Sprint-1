using System.Diagnostics;
using System.Text.Json;
using ClyvoCare.API.Diagnostics;
using ClyvoCare.API.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace ClyvoCare.IntegrationTests;

public sealed class TraceCorrelationTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Exception_UsesSameTraceInLogAndErrorAndRestoresLogContext(string environment)
    {
        var sink = new TestLogSink();
        using var logger = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();
        using var logging = new SerilogLoggerFactory(logger);
        var handler = new GlobalExceptionHandler(logging.CreateLogger<GlobalExceptionHandler>(),
            Mock.Of<IHostEnvironment>(host => host.EnvironmentName == environment));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        using var parent = new Activity("error-parent").SetIdFormat(ActivityIdFormat.W3C).Start();
        var middleware = new TraceContextMiddleware(async http =>
        {
            await handler.TryHandleAsync(http, new Exception("private-error-value"), default);
        });
        using (LogContext.PushProperty("TraceId", "outer-scope"))
        {
            await middleware.InvokeAsync(context);
            logger.Information("After request");
        }

        Assert.Same(parent, Activity.Current);
        Assert.Equal(ActivityStatusCode.Error, parent.Status);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Equal(parent.TraceId.ToString(), context.TraceIdentifier);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(parent.TraceId.ToString(), json.RootElement.GetProperty("traceId").GetString());
        Assert.DoesNotContain("private-error-value", json.RootElement.ToString());
        Assert.Equal(parent.TraceId.ToString(), ((ScalarValue)sink.Events[0].Properties["TraceId"]).Value);
        Assert.Equal("outer-scope", ((ScalarValue)sink.Events[1].Properties["TraceId"]).Value);
        Assert.Empty(parent.Events);
        Assert.Equal(typeof(Exception).FullName, parent.GetTagItem("error.type"));
    }

    private sealed class TestLogSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
