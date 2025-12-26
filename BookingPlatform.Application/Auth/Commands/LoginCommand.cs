using BookingPlatform.Application.Auth;
using MediatR;

namespace BookingPlatform.Application.Auth.Commands;

public sealed record LoginCommand(
    string Username,
    string Password
) : IRequest<AuthResultDto>;

