namespace RTSec.Kryptonian.Application.DTOs;

public record AuthStatusDto(string Mode, string? Username, string? Role, string? UserId);

public record LoginRequestDto(string Username, string Password);

public record SetupRequestDto(string Username, string Email, string Password);

public record AuthResponseDto(string Token, DateTimeOffset ExpiresAt, string Role, string Username, string UserId);
