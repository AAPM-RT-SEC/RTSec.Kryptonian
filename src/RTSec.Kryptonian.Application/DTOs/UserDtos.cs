namespace RTSec.Kryptonian.Application.DTOs;

public record UserDto(
    string Id,
    string Username,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateUserDto(string Username, string Email, string Password, string Role);

public record UpdateUserDto(string? Role, bool? IsActive, string? NewPassword);

public record ApiKeyDto(
    string Id,
    string Name,
    string Prefix,
    string Role,
    string OwnerId,
    string OwnerUsername,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    DateTime CreatedAt);

public record GenerateApiKeyRequestDto(string Name, DateTimeOffset? ExpiresAt);

public record GenerateApiKeyResponseDto(
    string Id,
    string Name,
    string Prefix,
    string RawKey,
    string Role,
    DateTimeOffset? ExpiresAt,
    DateTime CreatedAt);
