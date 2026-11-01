using System.Globalization;
using ClyvoCare.Application.Services.Interfaces;

namespace ClyvoCare.API.Auth;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public long OwnerId
    {
        get
        {
            var user = accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true
                || !long.TryParse(user.FindFirst("ownerId")?.Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var ownerId) || ownerId <= 0)
                throw new UnauthorizedAccessException("An authenticated owner is required.");
            return ownerId;
        }
    }

    public bool IsAdmin
    {
        get
        {
            var user = accessor.HttpContext?.User;
            return user?.Identity?.IsAuthenticated == true && user.IsInRole("ADMIN");
        }
    }
}
