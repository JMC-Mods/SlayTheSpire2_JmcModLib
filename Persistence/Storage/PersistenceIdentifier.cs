using System.Text;

namespace JmcModLib.Persistence.Storage;

internal static class PersistenceIdentifier
{
    public static string SanitizeKey(string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return "data";
        }

        var builder = new StringBuilder(rawKey.Length);
        foreach (char ch in rawKey.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('_');
            }
        }

        string sanitized = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "data" : sanitized;
    }

    public static string SanitizePathSegment(string rawSegment)
    {
        string sanitized = SanitizeKey(rawSegment);
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "mod" : sanitized;
    }
}
