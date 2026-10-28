namespace ClyvoCare.Application.Services.Interfaces;

public interface ICurrentUser
{
    long OwnerId { get; }
    bool IsAdmin { get; }
}
