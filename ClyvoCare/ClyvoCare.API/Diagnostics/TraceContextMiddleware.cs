using System.Diagnostics;
using Serilog.Context;

namespace ClyvoCare.API.Diagnostics;

public sealed class TraceContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.TraceIdentifier = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        var traceId = context.TraceIdentifier;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Trace-Id"] = traceId;
            return Task.CompletedTask;
        });
        using (LogContext.PushProperty("TraceId", traceId))
            await next(context);
    }
}
