using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TodoAPI.Models;

namespace TodoAPI.Helpers;

public static class TokenService
{
    public static string CreateToken(AppUser user, IConfiguration config)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName!)
        };
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
        claims:claims,
        expires: DateTime.UtcNow.AddHours(1),
        signingCredentials:creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}