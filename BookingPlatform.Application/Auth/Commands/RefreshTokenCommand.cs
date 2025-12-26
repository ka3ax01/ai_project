using BookingPlatform.Application.Auth;
using MediatR;

namespace BookingPlatform.Application.Auth.Commands;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResultDto>;

