using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Users.Application.Abstractions;
using Users.Domain.Entities;
using Users.Domain.Enums;

namespace Users.Infrastructure.Authentication;

public class JwtTokenProvider : ITokenProvider
{
    private readonly IConfiguration _configuration;
    public JwtTokenProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    public string Create(User user)
    {
        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException(
                "JWT secret key is not configured."
            );
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey)
            ),
            SecurityAlgorithms.HmacSha256
        );
        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()
            ),
            new Claim(
                ClaimTypes.Email,
                user.Email
            ),
            new Claim(
                ClaimTypes.Name,
                user.Username
            ),
            new Claim(
                ClaimTypes.Role,
                user.Role.ToString()
            )
        };
        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            claims.Add(new Claim(
                "full_name",
                user.FullName
            ));
        }
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(
                GetTokenLifetimeHours()),
            signingCredentials: credentials
        );
        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
    private int GetTokenLifetimeHours()
    {
        var configured = _configuration.GetValue(
            "Jwt:TokenLifetimeHours",
            24);
        return configured > 0 ? configured : 24;
    }
}
