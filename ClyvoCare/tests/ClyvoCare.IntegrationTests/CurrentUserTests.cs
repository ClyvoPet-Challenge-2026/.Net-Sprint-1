using System.Security.Claims;
using ClyvoCare.API.Auth;
using Microsoft.AspNetCore.Http;

namespace ClyvoCare.IntegrationTests;

public class CurrentUserTests
{
    private static CurrentUser Create(string id, string role, string? authenticationType = "test") =>
        new(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("ownerId", id), new Claim("role", role)], authenticationType, "sub", "role"))
            }
        });

    [Theory]
    [InlineData("OWNER", false)]
    [InlineData("ADMIN", true)]
    public void AuthenticatedIdentity_UsesOriginalOwnerAndRoleClaims(string role, bool admin)
    {
        var user = Create("7", role);
        Assert.Equal(7, user.OwnerId);
        Assert.Equal(admin, user.IsAdmin);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("invalid")]
    [InlineData("9223372036854775808")]
    public void InvalidOwnerId_IsRejected(string id)
    {
        Assert.Throws<UnauthorizedAccessException>(() => Create(id, "OWNER").OwnerId);
    }

    [Fact]
    public void MissingOrAnonymousIdentity_IsNotAnAdministrator()
    {
        var missing = new CurrentUser(new HttpContextAccessor());
        Assert.False(missing.IsAdmin);
        Assert.Throws<UnauthorizedAccessException>(() => missing.OwnerId);
        var anonymous = Create("7", "ADMIN", null);
        Assert.False(anonymous.IsAdmin);
        Assert.Throws<UnauthorizedAccessException>(() => anonymous.OwnerId);
    }
}
