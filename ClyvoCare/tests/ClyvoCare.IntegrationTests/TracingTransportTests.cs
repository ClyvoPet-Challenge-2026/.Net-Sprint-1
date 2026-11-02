using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ClyvoCare.API.Extensions;
using ClyvoCare.Application.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace ClyvoCare.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class TracingTransportTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task HttpClient_PropagatesTraceAndOmitsUrlAndHeadersFromSpans()
    {
        using var api = factory.CreateClient();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = Receive(listener);
        using var parent = new Activity("outbound-parent").SetIdFormat(ActivityIdFormat.W3C).Start();
        parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.Add("Authorization", "Bearer private-token-value");
        var response = await client.GetAsync($"http://127.0.0.1:{port}/lookup?token=private-query-value");
        var (headers, _) = await received;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Same(parent, Activity.Current);
        var span = Assert.Single(factory.Traces, activity => activity.TraceId == parent.TraceId && activity.Kind == ActivityKind.Client);
        Assert.Equal(parent.SpanId, span.ParentSpanId);
        Assert.Equal($"00-{parent.TraceId}-{span.SpanId}-01", headers["traceparent"]);
        Assert.Null(span.GetTagItem("url.full"));
        var values = string.Join(" ", span.TagObjects.Select(tag => tag.Value));
        Assert.DoesNotContain("private-token-value", values);
        Assert.DoesNotContain("private-query-value", values);
    }

    [Fact]
    public async Task Otlp_ConfiguredEndpoint_ReceivesProtobufTrace()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = Receive(listener);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = $"http://127.0.0.1:{port}",
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
            ["OTEL_EXPORTER_OTLP_HEADERS"] = "x-test-key=local-test-key",
            ["OTEL_EXPORTER_OTLP_TIMEOUT"] = "3000"
        }).Build();
        using var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration)
            .AddClyvoCareTracing(configuration).BuildServiceProvider();
        var provider = services.GetRequiredService<TracerProvider>();
        using (var activity = ApplicationTelemetry.Source.StartActivity("OTLP transport test"))
            Assert.NotNull(activity);
        Assert.True(provider.ForceFlush(5000));
        var (headers, body) = await received;
        Assert.StartsWith("POST /v1/traces ", headers["request-line"]);
        Assert.Equal("application/x-protobuf", headers["Content-Type"]);
        Assert.Equal("local-test-key", headers["x-test-key"]);
        Assert.Contains("OTLP transport test", Encoding.UTF8.GetString(body));
    }

    private static async Task<(Dictionary<string, string> Headers, byte[] Body)> Receive(TcpListener listener)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var connection = await listener.AcceptTcpClientAsync(cancellation.Token);
        await using var stream = connection.GetStream();
        var bytes = new List<byte>();
        var current = new byte[1];
        while (bytes.Count < 32768)
        {
            await stream.ReadExactlyAsync(current, cancellation.Token);
            bytes.Add(current[0]);
            if (bytes.Count >= 4 && bytes.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) break;
        }
        var lines = Encoding.ASCII.GetString(bytes.ToArray()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["request-line"] = lines[0] };
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(':', 2);
            headers.Add(parts[0], parts[1].Trim());
        }
        var length = headers.TryGetValue("Content-Length", out var value) ? int.Parse(value) : 0;
        var body = new byte[length];
        await stream.ReadExactlyAsync(body, cancellation.Token);
        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/x-protobuf\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), cancellation.Token);
        return (headers, body);
    }
}
