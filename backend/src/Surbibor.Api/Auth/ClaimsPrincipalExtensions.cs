using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Surbibor.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No user id claim present on the current principal.");

        return Guid.Parse(value);
    }
}
