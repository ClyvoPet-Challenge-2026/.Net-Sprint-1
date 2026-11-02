using System.Diagnostics;

namespace ClyvoCare.Infrastructure.Diagnostics;

public static class PersistenceTelemetry
{
    public static readonly ActivitySource Source = new("ClyvoCare.Infrastructure");
}
