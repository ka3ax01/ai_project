using BookingPlatform.Application.Auth;
using BookingPlatform.Domain.Users;

namespace BookingPlatform.Application.Auth;

public interface IJwtTokenService
{
    AuthResultDto GenerateTokens(User user);
    AuthResultDto RefreshTokens(string refreshToken);
}

