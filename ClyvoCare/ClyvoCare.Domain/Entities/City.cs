using ClyvoCare.Domain.Common;

namespace ClyvoCare.Domain.Entities;

public sealed class City : BaseEntity
{
    public string Name { get; private set; } = null!;

    public long StateId { get; private set; }

    public State State { get; private set; } = null!;

    private City()
    {
    }
}
