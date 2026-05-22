using ClyvoCare.Domain.Common;

namespace ClyvoCare.Domain.Entities;

public sealed class State : BaseEntity
{
    public string Name { get; private set; } = null!;

    public string UF { get; private set; } = null!;

    private State()
    {
    }
}
