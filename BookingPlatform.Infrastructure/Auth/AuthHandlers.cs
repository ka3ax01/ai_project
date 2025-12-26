using BookingPlatform.Application.Auth;
using BookingPlatform.Application.Auth.Commands;
using BookingPlatform.Domain.Enums;
using BookingPlatform.Domain.Users;
using BookingPlatform.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Infrastructure.Auth;

public sealed class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, AuthResultDto>
{
    private readonly AppDbContext _dbContext;
    private readonly IJwtTokenService _jwtTokenService;

    public RegisterUserCommandHandler(AppDbContext dbContext, IJwtTokenService jwtTokenService)
    {
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResultDto> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Users.AnyAsync(u => u.Username == request.Username, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("User already exists");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role = UserRole.User,
            CreatedBy = UserConstants.SystemUserId
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return _jwtTokenService.GenerateTokens(user);
    }
}

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResultDto>
{
    private readonly AppDbContext _dbContext;
    private readonly IJwtTokenService _jwtTokenService;

    public LoginCommandHandler(AppDbContext dbContext, IJwtTokenService jwtTokenService)
    {
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidOperationException("Invalid credentials");
        }

        return _jwtTokenService.GenerateTokens(user);
    }
}

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResultDto>
{
    private readonly IJwtTokenService _jwtTokenService;

    public RefreshTokenCommandHandler(IJwtTokenService jwtTokenService)
    {
        _jwtTokenService = jwtTokenService;
    }

    public Task<AuthResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var result = _jwtTokenService.RefreshTokens(request.RefreshToken);
        return Task.FromResult(result);
    }
}
