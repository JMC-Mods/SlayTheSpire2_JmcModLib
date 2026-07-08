using MegaCrit.Sts2.Core.Localization;
using System.Reflection;

namespace JmcModLib.Utils;

internal static class GameRestartText
{
    private const string KeyPrefix = "EXTENSION.JMCMODLIB.RESTART";
    private static readonly Assembly ThisAssembly = typeof(GameRestartText).Assembly;

    public static string ConfirmTitle() => Resolve("CONFIRM_TITLE", "Restart game?");

    public static string ConfirmBody() => Resolve(
        "CONFIRM_BODY",
        "The game will save current settings, exit, and start again.");

    public static string ConfirmButton() => Resolve("CONFIRM_BUTTON", "Restart");

    public static string CancelButton() => Resolve("CANCEL_BUTTON", "Cancel");

    public static string CloseButton() => Resolve("CLOSE_BUTTON", "Close");

    public static string UnsupportedTitle() => Resolve("UNSUPPORTED_TITLE", "Restart unavailable");

    public static string UnsupportedBody(string platform)
    {
        return Resolve(
            "UNSUPPORTED_BODY",
            $"Automatic restart is not supported on {platform}. Please restart the game manually.",
            loc => loc.Add("platform", platform));
    }

    private static string Resolve(string key, string fallback, Action<LocString>? configure = null)
    {
        return L10n.Resolve(
            $"{KeyPrefix}.{key}",
            fallback,
            L10n.DefaultTable,
            ThisAssembly,
            configure);
    }
}
