using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;

namespace RTSec.Kryptonian.Domain.Services;

/// <summary>
/// Provides hostname matching logic for EST profile routing.
/// </summary>
public static class HostnameMatcher
{
    /// <summary>
    /// Checks if a hostname matches any of the profile's configured hostnames.
    /// </summary>
    /// <param name="profile">The EST profile to check against.</param>
    /// <param name="hostname">The hostname to match (should be lowercase, without port).</param>
    /// <returns>True if the hostname matches the profile's configuration.</returns>
    public static bool Matches(EstProfile profile, string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            return false;

        hostname = NormalizeHostname(hostname);

        return profile.HostnameMatchType switch
        {
            HostnameMatchType.Exact => MatchExact(profile.Hostnames, hostname),
            HostnameMatchType.Suffix => MatchSuffix(profile.Hostnames, hostname),
            HostnameMatchType.Wildcard => MatchWildcard(profile.Hostnames, hostname, profile.AllowedWildcardSuffix),
            _ => false
        };
    }

    /// <summary>
    /// Normalizes a hostname for matching (lowercase, no port, trimmed).
    /// </summary>
    public static string NormalizeHostname(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            return string.Empty;

        hostname = hostname.Trim().ToLowerInvariant();

        // Remove port if present
        var colonIndex = hostname.LastIndexOf(':');
        if (colonIndex > 0 && colonIndex < hostname.Length - 1)
        {
            // Check if it's a port (all digits after colon)
            var potentialPort = hostname[(colonIndex + 1)..];
            if (potentialPort.All(char.IsDigit))
            {
                hostname = hostname[..colonIndex];
            }
        }

        return hostname;
    }

    /// <summary>
    /// Validates a hostname pattern for the given match type.
    /// </summary>
    /// <param name="pattern">The hostname pattern to validate.</param>
    /// <param name="matchType">The match type being used.</param>
    /// <returns>True if the pattern is valid for the match type.</returns>
    public static bool IsValidPattern(string pattern, HostnameMatchType matchType)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        return matchType switch
        {
            HostnameMatchType.Exact => IsValidExactHostname(pattern),
            HostnameMatchType.Suffix => IsValidSuffixPattern(pattern),
            HostnameMatchType.Wildcard => pattern == "*" || IsValidSuffixPattern(pattern),
            _ => false
        };
    }

    private static bool MatchExact(List<string> patterns, string hostname)
    {
        return patterns.Any(p =>
            string.Equals(NormalizeHostname(p), hostname, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchSuffix(List<string> patterns, string hostname)
    {
        foreach (var pattern in patterns)
        {
            var normalizedPattern = pattern.Trim().ToLowerInvariant();

            // Suffix patterns must start with a dot
            if (!normalizedPattern.StartsWith('.'))
                continue;

            // Check if hostname ends with the suffix
            if (hostname.EndsWith(normalizedPattern, StringComparison.OrdinalIgnoreCase))
                return true;

            // Also match if hostname equals the suffix without the leading dot
            // e.g., ".example.com" should match "example.com"
            if (hostname.Equals(normalizedPattern[1..], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool MatchWildcard(List<string> patterns, string hostname, string? allowedSuffix)
    {
        foreach (var pattern in patterns)
        {
            if (pattern == "*")
            {
                // If there's a suffix restriction, enforce it
                if (!string.IsNullOrWhiteSpace(allowedSuffix))
                {
                    var normalizedSuffix = allowedSuffix.Trim().ToLowerInvariant();
                    if (!normalizedSuffix.StartsWith('.'))
                        normalizedSuffix = "." + normalizedSuffix;

                    if (hostname.EndsWith(normalizedSuffix, StringComparison.OrdinalIgnoreCase) ||
                        hostname.Equals(normalizedSuffix[1..], StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                else
                {
                    // Unrestricted wildcard matches everything
                    return true;
                }
            }
            else
            {
                // Treat non-* patterns as suffix patterns in wildcard mode
                var normalizedPattern = pattern.Trim().ToLowerInvariant();
                if (!normalizedPattern.StartsWith('.'))
                    normalizedPattern = "." + normalizedPattern;

                if (hostname.EndsWith(normalizedPattern, StringComparison.OrdinalIgnoreCase) ||
                    hostname.Equals(normalizedPattern[1..], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsValidExactHostname(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname) || hostname.Length > 253)
            return false;

        // Must not start or end with dot/hyphen
        if (hostname.StartsWith('.') || hostname.StartsWith('-') ||
            hostname.EndsWith('.') || hostname.EndsWith('-'))
            return false;

        // Basic hostname character validation
        return hostname.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-');
    }

    private static bool IsValidSuffixPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern.Length > 254)
            return false;

        // Suffix patterns must start with a dot
        if (!pattern.StartsWith('.'))
            return false;

        // Must have at least a second-level domain (e.g., ".example.com" not just ".com")
        var withoutLeadingDot = pattern[1..];
        if (!withoutLeadingDot.Contains('.'))
            return false;

        return IsValidExactHostname(withoutLeadingDot);
    }
}
