namespace JmcModLib.UI.PauseMenu;

internal static class PauseMenuLocalization
{
    public const string KeyPrefix = "EXTENSION.JMCMODLIB.PAUSE_MENU";

    private const string TextSuffix = "TEXT";

    public static string GetText(PauseMenuButtonEntry entry)
    {
        return L10n.ResolveAny(
            [entry.TextKey, BuildTextKey(entry)],
            entry.Text,
            entry.LocTable,
            entry.Assembly);
    }

    public static string BuildTextKey(PauseMenuButtonEntry entry)
    {
        return $"{KeyPrefix}.{NormalizeKeySegment(entry.ModId)}.{NormalizeKeySegment(entry.Key)}.{TextSuffix}";
    }

    private static string NormalizeKeySegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "_";
        }

        string trimmed = value.Trim().Replace('/', '.').Replace('\\', '.');
        return string.Join("_", trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
