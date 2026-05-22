using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Application.Services;

public interface IAuthService
{
    Task<bool> IsSetupRequiredAsync(CancellationToken ct = default);
    Task<AuthStatusDto> GetStatusAsync(bool isAuthenticated, string? authenticatedUsername, string? authenticatedRole, string? authenticatedUserId, CancellationToken ct = default);
    Task<AuthResponseDto> BootstrapAsync(SetupRequestDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    // Used as dev fallback when no JWT secret is configured. Not secure for production.
    private const string DevFallbackSecret = "kryptonian-dev-secret-not-for-production-32chars";

    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUnitOfWork uow, IConfiguration config, ILogger<AuthService> logger)
    {
        _uow = uow;
        _config = config;
        _logger = logger;
    }

    public async Task<bool> IsSetupRequiredAsync(CancellationToken ct = default)
        => !await _uow.Users.AnyAsync(ct);

    public async Task<AuthStatusDto> GetStatusAsync(
        bool isAuthenticated,
        string? authenticatedUsername,
        string? authenticatedRole,
        string? authenticatedUserId,
        CancellationToken ct = default)
    {
        if (!await _uow.Users.AnyAsync(ct))
            return new AuthStatusDto("setup", null, null, null);

        if (isAuthenticated)
            return new AuthStatusDto("ok", authenticatedUsername, authenticatedRole, authenticatedUserId);

        return new AuthStatusDto("login", null, null, null);
    }

    public async Task<AuthResponseDto> BootstrapAsync(SetupRequestDto dto, CancellationToken ct = default)
    {
        if (await _uow.Users.AnyAsync(ct))
            throw new InvalidOperationException("Setup has already been completed. Use login instead.");

        if (string.IsNullOrWhiteSpace(dto.Username))
            throw new ArgumentException("Username is required.");
        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ArgumentException("Email is required.");
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");

        var user = new User
        {
            Username = dto.Username.Trim(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12),
            Role = UserRole.SystemAdmin,
            IsActive = true
        };

        _uow.Users.Add(user);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("First admin account created: {Username}", user.Username);
        return BuildTokenResponse(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            throw new UnauthorizedAccessException("Invalid credentials.");

        var user = await _uow.Users.GetByUsernameAsync(dto.Username.Trim(), ct);
        if (user == null || !user.IsActive)
            throw new UnauthorizedAccessException("Invalid credentials.");

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        return BuildTokenResponse(user);
    }

    private AuthResponseDto BuildTokenResponse(User user)
    {
        var jwtSecret = _config["Kryptonian:Auth:JwtSecret"]
            ?? Environment.GetEnvironmentVariable("KRYPTONIAN__AUTH__JWTSECRET");

        if (string.IsNullOrEmpty(jwtSecret))
        {
            _logger.LogWarning(
                "KRYPTONIAN__AUTH__JWTSECRET is not configured. Using dev fallback secret. " +
                "Set this environment variable before deploying to production.");
            jwtSecret = DevFallbackSecret;
        }

        var lifetimeHours = int.TryParse(_config["Kryptonian:Auth:JwtLifetimeHours"], out var h) ? h : 8;
        var expiresAt = DateTimeOffset.UtcNow.AddHours(lifetimeHours);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("role", user.Role.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: "kryptonian",
            audience: "kryptonian-ui",
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        return new AuthResponseDto(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            user.Role.ToString(),
            user.Username,
            user.Id.ToString());
    }

    public static string ComputeKeyHash(string rawKey)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(rawKey));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
