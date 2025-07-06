using System.Security.Claims;

namespace TodoAPI.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string GetUserId(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? throw new UnauthorizedAccessException("User ID claim not found");
    }

    public static string GetUsername(this ClaimsPrincipal user)
    {
        return user.Identity?.Name
               ?? throw new UnauthorizedAccessException("Username claim not found");
    }
}