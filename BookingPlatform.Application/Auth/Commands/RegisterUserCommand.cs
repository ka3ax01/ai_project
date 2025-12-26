using BookingPlatform.Application.Auth;
using MediatR;

namespace BookingPlatform.Application.Auth.Commands;

public sealed record RegisterUserCommand(
    string Username,
    string Email,
    string Password
) : IRequest<AuthResultDto>;

