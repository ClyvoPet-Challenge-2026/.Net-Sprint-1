using System.Globalization;
using ClyvoCare.Application.Services.Interfaces;

namespace ClyvoCare.API.Auth;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public long OwnerId => long.TryParse(accessor.HttpContext?.User.FindFirst("ownerId")?.Value,
        NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
        ? id : throw new UnauthorizedAccessException("Identidade do tutor ausente.");

    public bool IsAdmin => accessor.HttpContext?.User.IsInRole("ADMIN") == true;
}
