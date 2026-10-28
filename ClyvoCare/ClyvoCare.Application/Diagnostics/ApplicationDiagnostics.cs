using System.Diagnostics;

namespace ClyvoCare.Application.Diagnostics;

/// <summary>Fonte de spans das operações da camada, sem dados pessoais nos atributos.</summary>
public static class ApplicationDiagnostics
{
    public const string SourceName = "ClyvoCare.Application";
    public static readonly ActivitySource Source = new(SourceName);
}
