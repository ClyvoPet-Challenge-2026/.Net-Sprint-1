using ClyvoCare.Infrastructure.Diagnostics;
using ClyvoCare.Application.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClyvoCare.Infrastructure.Persistence.Repositories;

public sealed class ClinicalIdentityRepository(ClyvoCareContext context) : IClinicalIdentityRepository
{
    public bool IsActiveOwner(long ownerId, string email, string role)
    {
        using var activity = PersistenceDiagnostics.Source.StartActivity("ClinicalIdentityRepository.IsActiveOwner");
        return context.OwnerReferences.AsNoTracking().Count(o => o.Id == ownerId && o.Email == email
            && o.Enabled == "Y" && o.RoleName == role) > 0;
    }

    public long? GetPetOwnerId(long petId)
    {
        using var activity = PersistenceDiagnostics.Source.StartActivity("ClinicalIdentityRepository.GetPetOwnerId");
        return context.PetReferences.AsNoTracking()
            .Where(p => p.Id == petId).Select(p => (long?)p.OwnerId).FirstOrDefault();
    }
}
