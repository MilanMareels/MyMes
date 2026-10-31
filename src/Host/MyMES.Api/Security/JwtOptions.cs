using System.Security.Cryptography;

namespace MyMES.Api.Security;

/// <summary>Settings for the bearer tokens the API issues and validates.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "MyMES";

    public string Audience { get; set; } = "MyMES";

    /// <summary>
    /// Signing key, at least 32 bytes.
    /// </summary>
    /// <remarks>
    /// Never committed. Supply it through <c>Jwt__Key</c> in the environment, or user-secrets
    /// during development. In Development only, a missing key is replaced by a random one that
    /// lives for the lifetime of the process, which invalidates every token on restart.
    /// </remarks>
    public string? Key { get; set; }

    public int ExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// Returns the configured key, or generates an ephemeral one for development.
    /// </summary>
    /// <param name="isDevelopment">Whether the host is running in the Development environment.</param>
    /// <param name="generated">True when the returned key was generated rather than configured.</param>
    /// <exception cref="InvalidOperationException">
    /// No key is configured outside Development, or the configured key is too short.
    /// </exception>
    public string ResolveKey(bool isDevelopment, out bool generated)
    {
        generated = false;

        if (!string.IsNullOrWhiteSpace(Key))
        {
            // 256 bits is the minimum for HMAC-SHA256; a shorter key is rejected rather than padded.
            if (Key.Length < 32)
            {
                throw new InvalidOperationException(
                    $"{SectionName}:Key must be at least 32 characters. Generate one with "
                    + "'openssl rand -base64 48' and set it through the environment.");
            }

            return Key;
        }

        if (!isDevelopment)
        {
            throw new InvalidOperationException(
                $"{SectionName}:Key is not configured. Set Jwt__Key in the environment before "
                + "starting the API outside Development.");
        }

        generated = true;
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    }
}
