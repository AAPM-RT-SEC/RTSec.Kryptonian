using System.Security.Cryptography;
using BCrypt.Net;
using Microsoft.Extensions.Logging;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Application.Services;

public interface IUserService
{
    Task<IEnumerable<UserDto>> ListUsersAsync(CancellationToken ct = default);
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserDto> CreateUserAsync(CreateUserDto dto, CancellationToken ct = default);
    Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserDto dto, CancellationToken ct = default);
    Task<bool> DeleteUserAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<ApiKeyDto>> GetApiKeysAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<ApiKeyDto>> GetAllApiKeysAsync(CancellationToken ct = default);
    Task<GenerateApiKeyResponseDto?> GenerateApiKeyAsync(Guid userId, GenerateApiKeyRequestDto dto, CancellationToken ct = default);
    Task<bool> RevokeApiKeyAsync(Guid keyId, Guid requestingUserId, bool isSystemAdmin, CancellationToken ct = default);
}

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UserService> _logger;

    public UserService(IUnitOfWork uow, ILogger<UserService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<IEnumerable<UserDto>> ListUsersAsync(CancellationToken ct = default)
    {
        var users = await _uow.Users.GetAllAsync(ct);
        return users.Select(ToDto);
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(id, ct);
        return user == null ? null : ToDto(user);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Username))
            throw new ArgumentException("Username is required.");
        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ArgumentException("Email is required.");
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");

        if (!Enum.TryParse<UserRole>(dto.Role, ignoreCase: true, out var role))
            throw new ArgumentException($"Invalid role '{dto.Role}'. Valid: Standard, DeviceAdmin, SystemAdmin.");

        var existing = await _uow.Users.GetByUsernameAsync(dto.Username.Trim(), ct);
        if (existing != null)
            throw new InvalidOperationException($"Username '{dto.Username}' is already taken.");

        var existingEmail = await _uow.Users.GetByEmailAsync(dto.Email.Trim().ToLowerInvariant(), ct);
        if (existingEmail != null)
            throw new InvalidOperationException($"Email '{dto.Email}' is already registered.");

        var user = new User
        {
            Username = dto.Username.Trim(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12),
            Role = role,
            IsActive = true
        };

        _uow.Users.Add(user);
        await _uow.SaveChangesAsync(ct);
        return ToDto(user);
    }

    public async Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserDto dto, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(id, ct);
        if (user == null) return null;

        if (!string.IsNullOrWhiteSpace(dto.Role))
        {
            if (!Enum.TryParse<UserRole>(dto.Role, ignoreCase: true, out var role))
                throw new ArgumentException($"Invalid role '{dto.Role}'.");
            user.Role = role;
        }

        if (dto.IsActive.HasValue)
            user.IsActive = dto.IsActive.Value;

        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            if (dto.NewPassword.Length < 8)
                throw new ArgumentException("Password must be at least 8 characters.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 12);
        }

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);
        return ToDto(user);
    }

    public async Task<bool> DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(id, ct);
        if (user == null) return false;

        // Prevent deleting the last SystemAdmin
        if (user.Role == UserRole.SystemAdmin)
        {
            var allUsers = await _uow.Users.GetAllAsync(ct);
            var adminCount = allUsers.Count(u => u.Role == UserRole.SystemAdmin && u.IsActive);
            if (adminCount <= 1)
                throw new InvalidOperationException("Cannot delete the last active system administrator.");
        }

        _uow.Users.Delete(user);
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IEnumerable<ApiKeyDto>> GetApiKeysAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user == null) return Enumerable.Empty<ApiKeyDto>();

        var keys = await _uow.ApiKeys.GetByUserIdAsync(userId, ct);
        return keys.Select(k => ToKeyDto(k, user));
    }

    public async Task<IEnumerable<ApiKeyDto>> GetAllApiKeysAsync(CancellationToken ct = default)
    {
        var keys = await _uow.ApiKeys.GetAllActiveAsync(ct);
        return keys.Select(k => ToKeyDto(k, k.User));
    }

    public async Task<GenerateApiKeyResponseDto?> GenerateApiKeyAsync(
        Guid userId, GenerateApiKeyRequestDto dto, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user == null) return null;

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("API key name is required.");

        // Generate: kry_ + base64url(32 random bytes)
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var rawKey = "kry_" + Convert.ToBase64String(randomBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var prefix = rawKey[..Math.Min(12, rawKey.Length)];
        var keyHash = AuthService.ComputeKeyHash(rawKey);

        var apiKey = new ApiKey
        {
            Name = dto.Name.Trim(),
            KeyHash = keyHash,
            Prefix = prefix,
            UserId = userId,
            Role = user.Role,
            ExpiresAt = dto.ExpiresAt
        };

        _uow.ApiKeys.Add(apiKey);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("API key '{Name}' generated for user {Username} (prefix: {Prefix})",
            dto.Name, user.Username, prefix);

        return new GenerateApiKeyResponseDto(
            apiKey.Id.ToString(),
            apiKey.Name,
            apiKey.Prefix,
            rawKey,
            apiKey.Role.ToString(),
            apiKey.ExpiresAt,
            apiKey.CreatedAt);
    }

    public async Task<bool> RevokeApiKeyAsync(Guid keyId, Guid requestingUserId, bool isSystemAdmin, CancellationToken ct = default)
    {
        var key = await _uow.ApiKeys.GetByIdAsync(keyId, ct);
        if (key == null) return false;

        if (!isSystemAdmin && key.UserId != requestingUserId)
            throw new UnauthorizedAccessException("You can only revoke your own API keys.");

        key.RevokedAt = DateTimeOffset.UtcNow;
        _uow.ApiKeys.Update(key);
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    private static UserDto ToDto(User u) =>
        new(u.Id.ToString(), u.Username, u.Email, u.Role.ToString(), u.IsActive, u.CreatedAt, u.UpdatedAt);

    private static ApiKeyDto ToKeyDto(ApiKey k, User? user) =>
        new(k.Id.ToString(), k.Name, k.Prefix, k.Role.ToString(),
            k.UserId.ToString(), user?.Username ?? "unknown",
            k.ExpiresAt, k.LastUsedAt, k.RevokedAt, k.CreatedAt);
}
