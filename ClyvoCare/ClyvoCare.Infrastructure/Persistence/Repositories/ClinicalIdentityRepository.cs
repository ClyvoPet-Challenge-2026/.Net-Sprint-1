using ClyvoCare.Application.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClyvoCare.Infrastructure.Persistence.Repositories;

public sealed class ClinicalIdentityRepository(ClyvoCareContext context) : IClinicalIdentityRepository
{
    public async Task<bool> IsActiveOwnerAsync(long ownerId, string email, string role,
        CancellationToken cancellationToken)
    {
        return await context.Owners.AsNoTracking().CountAsync(owner => owner.Id == ownerId
            && owner.Email == email && owner.RoleName == role && owner.Enabled == "Y", cancellationToken) > 0;
    }
}
