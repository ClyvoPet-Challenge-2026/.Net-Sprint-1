using ClyvoCare.Domain.Common;

namespace ClyvoCare.Domain.Entities;

public sealed class PetReference : BaseEntity
{
    public long OwnerId { get; private set; }
    public OwnerReference Owner { get; private set; } = null!;

    private PetReference()
    {
    }
}
