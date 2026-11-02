using System.Diagnostics;

namespace ClyvoCare.Application.Diagnostics;

public static class ApplicationTelemetry
{
    public static readonly ActivitySource Source = new("ClyvoCare.Application");
}
