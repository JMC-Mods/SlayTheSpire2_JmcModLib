using MegaCrit.Sts2.Core.Localization;

namespace JmcModLib.Config.UI;

internal static class ModSettingsText
{
    private const string KeyPrefix = "EXTENSION.JMCMODLIB.UI";

    public static string TabLabel() => Resolve("MOD_SETTINGS_TAB", "Mod Settings");

    public static string Title() => Resolve("MOD_SETTINGS_TITLE", "Mod Settings");

    public static string Description() => Resolve(
        "MOD_SETTINGS_DESCRIPTION",
        "Adjust registered mod configuration here. Changes are saved immediately.");

    public static string ChangesSavedImmediately() => Resolve(
        "CHANGES_SAVED_IMMEDIATELY",
        "Changes are saved immediately.");

    public static string NoConfigMods() => Resolve(
        "NO_CONFIG_MODS",
        "No registered mods currently have configurable entries.");

    public static string NoManagedAssembly() => Resolve(
        "NO_MANAGED_ASSEMBLY",
        "This mod has no loaded managed assembly, so its config cannot be shown.");

    public static string NoConfigEntries() => Resolve(
        "NO_CONFIG_ENTRIES",
        "This mod currently has no visible config entries.");

    public static string AuthorLabel() => Resolve("AUTHOR_LABEL", "Author");

    public static string VersionLabel() => Resolve("VERSION_LABEL", "Version");

    public static string RestartRequired() => Resolve(
        "RESTART_REQUIRED",
        "Requires restart to fully apply.");

    public static string Expand() => Resolve("EXPAND", "Expand");

    public static string Collapse() => Resolve("COLLAPSE", "Collapse");

    public static string ExpandAll() => Resolve("EXPAND_ALL", "Expand All");

    public static string CollapseAll() => Resolve("COLLAPSE_ALL", "Collapse All");

    public static string ResetMod() => Resolve("RESET_MOD", "Reset This Mod");

    public static string Reset() => Resolve("RESET", "Reset");

    public static string Close() => Resolve("CLOSE", "Close");

    public static string KeybindListening() => Resolve("KEYBIND_LISTENING", "Press a key or button...");

    public static string KeybindUnbound() => Resolve("KEYBIND_UNBOUND", "Unbound");

    public static string SteamInputManaged() => Resolve("STEAM_INPUT_MANAGED", "Bind in Steam Input");

    public static string ColorSelect() => Resolve("COLOR_SELECT", "Select");

    public static string SecretSetButton() => Resolve("SECRET_SET_BUTTON", "Set / Update");

    public static string SecretClearButton() => Resolve("SECRET_CLEAR_BUTTON", "Clear");

    public static string SecretStatusMissing() => Resolve("SECRET_STATUS_MISSING", "Not saved");

    public static string SecretStatusSaved() => Resolve("SECRET_STATUS_SAVED", "Saved");

    public static string SecretStatusUnavailable() => Resolve(
        "SECRET_STATUS_UNAVAILABLE",
        "Secure storage is not supported on this platform.");

    public static string SecretStatusWeak() => Resolve(
        "SECRET_STATUS_WEAK",
        "Only weak file protection is available.");

    public static string SecretStatusSavedWeak() => Resolve(
        "SECRET_STATUS_SAVED_WEAK",
        "Saved with weak file protection.");

    public static string SecretStatusAccessDenied() => Resolve(
        "SECRET_STATUS_ACCESS_DENIED",
        "Access denied.");

    public static string SecretStatusWeakNotAllowed() => Resolve(
        "SECRET_STATUS_WEAK_NOT_ALLOWED",
        "Weak file protection is not allowed for this Secret.");

    public static string SecretStatusBackendError() => Resolve(
        "SECRET_STATUS_BACKEND_ERROR",
        "Secret backend error.");

    public static string SecretInputConfirm() => Resolve("SECRET_INPUT_CONFIRM", "Save");

    public static string SecretInputCancel() => Resolve("SECRET_INPUT_CANCEL", "Cancel");

    public static string SecretInputPlaceholder() => Resolve(
        "SECRET_INPUT_PLACEHOLDER",
        "Enter secret value");

    public static string SecretInputEmpty() => Resolve(
        "SECRET_INPUT_EMPTY",
        "Secret value cannot be empty.");

    public static string SecretInputUnavailableTitle() => Resolve(
        "SECRET_INPUT_UNAVAILABLE_TITLE",
        "Secret storage unavailable");

    public static string SecretInputUnavailableBody() => Resolve(
        "SECRET_INPUT_UNAVAILABLE_BODY",
        "This platform does not currently provide secure Secret storage for this entry.");

    public static string SecretInputWeakWarning() => Resolve(
        "SECRET_INPUT_WEAK_WARNING",
        "Current platform cannot use system secure storage. Saving will use weak file protection and is not recommended on shared devices.");

    public static string SecretClearTitle() => Resolve("SECRET_CLEAR_TITLE", "Clear Secret");

    public static string SecretClearBody(string name)
    {
        return Resolve(
            "SECRET_CLEAR_BODY",
            $"Clear the saved Secret for {name}?",
            loc => loc.Add("name", name));
    }

    public static string SecretSaveFailed(string status)
    {
        return Resolve(
            "SECRET_SAVE_FAILED",
            $"Failed to save Secret: {status}",
            loc => loc.Add("status", status));
    }

    public static string SecretClearFailed(string status)
    {
        return Resolve(
            "SECRET_CLEAR_FAILED",
            $"Failed to clear Secret: {status}",
            loc => loc.Add("status", status));
    }

    public static string ConfigTitle(string modName)
    {
        return Resolve(
            "CONFIG_TITLE",
            $"{modName} Config",
            loc => loc.Add("modName", modName));
    }

    public static string UnsupportedType(string typeName)
    {
        return Resolve(
            "UNSUPPORTED_TYPE",
            $"Unsupported type: {typeName}",
            loc => loc.Add("type", typeName));
    }

    private static string Resolve(string key, string fallback, Action<LocString>? configure = null)
    {
        return L10n.Resolve(
            $"{KeyPrefix}.{key}",
            fallback,
            L10n.DefaultTable,
            typeof(ModSettingsText).Assembly,
            configure);
    }
}
