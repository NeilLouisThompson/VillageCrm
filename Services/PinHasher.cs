using System.Security.Cryptography;

namespace VillageCrm.Services;

/// <summary>Salted PBKDF2 hashes for resident kiosk PINs. PINs are never stored in plain text.</summary>
public static class PinHasher
{
    private const int Iterations = 100_000;

    public static bool IsValidFormat(string pin) => pin.Length == 4 && pin.All(char.IsAsciiDigit);

    public static string Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string pin, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }
        var expected = Convert.FromBase64String(parts[1]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(pin, Convert.FromBase64String(parts[0]), Iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
