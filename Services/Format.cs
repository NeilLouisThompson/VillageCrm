using System.Globalization;

namespace VillageCrm.Services;

public static class Format
{
    public static string Money(decimal amount) => "R " + amount.ToString("N2", CultureInfo.InvariantCulture);

    public static string Date(DateTime? value) => value?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "";

    public static string When(DateTime? value) => value?.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture) ?? "";

    /// <summary>"LeftVillage" becomes "Left village".</summary>
    public static string Words<T>(T value) where T : struct, Enum
    {
        var text = value.ToString();
        var chars = new List<char>();
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && char.IsUpper(text[i]))
            {
                chars.Add(' ');
                chars.Add(char.ToLowerInvariant(text[i]));
            }
            else
            {
                chars.Add(text[i]);
            }
        }
        return new string(chars.ToArray());
    }
}
