using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace InfoClusMonitor.Api.Services.Auth;

public class TokenService(IConfiguration configuration)
{
    public Task<string> GenerateJwtToken(IEnumerable<Claim> claims, DateTime expirationDate)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var keyInBytes = Encoding.UTF8.GetBytes(configuration["Auth:SecurityKey"]!);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new(claims),
            Expires = expirationDate,
            SigningCredentials = new(new SymmetricSecurityKey(keyInBytes), SecurityAlgorithms.HmacSha256Signature)
        };
        var jwtToken = tokenHandler.CreateToken(tokenDescriptor);
        return Task.FromResult(tokenHandler.WriteToken(jwtToken));
    }
}
