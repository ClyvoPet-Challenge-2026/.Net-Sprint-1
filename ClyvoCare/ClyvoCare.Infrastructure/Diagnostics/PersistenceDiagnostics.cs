using System.Diagnostics;

namespace ClyvoCare.Infrastructure.Diagnostics;

/// <summary>Fonte de spans das operações da camada, sem dados pessoais nos atributos.</summary>
public static class PersistenceDiagnostics
{
    public const string SourceName = "ClyvoCare.Infrastructure";
    public static readonly ActivitySource Source = new(SourceName);
}
