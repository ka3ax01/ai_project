using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BookingPlatform.Application.Auth;
using BookingPlatform.Domain.Auth;
using BookingPlatform.Domain.Users;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BookingPlatform.Infrastructure.Auth;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly AppDbContext _dbContext;
    private readonly JwtOptions _options;

    public JwtTokenService(AppDbContext dbContext, IOptions<JwtOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public AuthResultDto GenerateTokens(User user)
    {
        var accessToken = CreateAccessToken(user);
        var refreshToken = CreateRefreshToken(user);

        return new AuthResultDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token
        };
    }

    public AuthResultDto RefreshTokens(string refreshToken)
    {
        var existing = _dbContext.RefreshTokens
            .Include(x => x)
            .FirstOrDefault(x => x.Token == refreshToken && !x.IsDeleted);

        if (existing is null || existing.IsRevoked || existing.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            throw new SecurityTokenException("Invalid refresh token");
        }

        existing.IsRevoked = true;
        _dbContext.SaveChanges();

        var user = _dbContext.Users.First(u => u.Id == existing.UserId);
        return GenerateTokens(user);
    }

    private string CreateAccessToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenLifetimeMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private RefreshToken CreateRefreshToken(User user)
    {
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = token,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenLifetimeDays),
            CreatedBy = user.Id
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        _dbContext.SaveChanges();

        return refreshToken;
    }
}
