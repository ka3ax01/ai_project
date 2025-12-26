using System.Security.Cryptography;
using System.Text;

namespace BookingPlatform.Infrastructure.Auth;

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        using var rng = RandomNumberGenerator.Create();
        Span<byte> salt = stackalloc byte[16];
        rng.GetBytes(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt.ToArray(), 100_000, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);

        var result = new byte[1 + salt.Length + hash.Length];
        result[0] = 1; // version
        salt.CopyTo(result.AsSpan(1));
        hash.CopyTo(result.AsSpan(1 + salt.Length));

        return Convert.ToBase64String(result);
    }

    public static bool Verify(string password, string storedHash)
    {
        var bytes = Convert.FromBase64String(storedHash);
        if (bytes.Length < 1 + 16 + 32)
        {
            return false;
        }

        var version = bytes[0];
        if (version != 1)
        {
            return false;
        }

        var salt = bytes.AsSpan(1, 16).ToArray();
        var stored = bytes.AsSpan(17).ToArray();

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        var computed = pbkdf2.GetBytes(32);

        return CryptographicOperations.FixedTimeEquals(stored, computed);
    }
}

