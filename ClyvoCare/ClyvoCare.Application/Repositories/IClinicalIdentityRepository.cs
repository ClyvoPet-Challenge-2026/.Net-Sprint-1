namespace ClyvoCare.Application.Repositories;

public interface IClinicalIdentityRepository
{
    Task<bool> IsActiveOwnerAsync(long ownerId, string email, string role, CancellationToken cancellationToken);
}
