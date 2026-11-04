using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AcxiomCRM.Services;

// Issues signed JWT access tokens for the REST API (10.1)
public interface ITokenService
{
    Task<(string Token, DateTime ExpiresAtUtc)> CreateTokenAsync(ApplicationUser user);
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly UserManager<ApplicationUser> _userManager;

    public TokenService(IConfiguration config, UserManager<ApplicationUser> userManager)
    {
        _config = config;
        _userManager = userManager;
    }

    public async Task<(string Token, DateTime ExpiresAtUtc)> CreateTokenAsync(ApplicationUser user)
    {
        var jwt = _config.GetSection("Jwt");
        var expires = DateTime.UtcNow.AddMinutes(jwt.GetValue("ExpiryMinutes", 60));

        // Only identity + role claims. No password, hash or other sensitive data.
        var claims = new List<Claim>

        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Email ?? user.UserName ?? ""),
            new(ClaimTypes.Email, user.Email ?? ""),
            new("full_name", user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        foreach (var role in await _userManager.GetRolesAsync(user))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt["Issuer"],
            Audience = jwt["Audience"],
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });

        return (token, expires);
    }
}