using ClyvoCare.Domain.Common;

namespace ClyvoCare.Domain.Entities;

/// <summary>Projeção de leitura do tutor mantido pela API Java.</summary>
public sealed class OwnerReference : BaseEntity
{
    public string Email { get; private set; } = null!;
    public string RoleName { get; private set; } = null!;
    public string Enabled { get; private set; } = null!;
    private OwnerReference() { }
}
