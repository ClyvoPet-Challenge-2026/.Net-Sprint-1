namespace ClyvoCare.Application.Repositories;

/// <summary>Consulta identidades e vínculos gerenciados pela Java, sem escrita.</summary>
public interface IClinicalIdentityRepository
{
    bool IsActiveOwner(long ownerId, string email, string role);
    long? GetPetOwnerId(long petId);
}
