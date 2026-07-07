namespace JmcModLib.Security.Backends;

internal static class SecretBackendSelector
{
    private static readonly Lazy<ISecretBackend> WindowsBackend = new(static () => new WindowsDpapiSecretBackend());
    private static readonly Lazy<ISecretBackend> WeakFileBackend = new(static () => new WeakFileSecretBackend());
    private static readonly ISecretBackend UnavailableBackend = new UnavailableSecretBackend(
        saveStatus: JmcSecretWriteStatus.Unavailable);
    private static readonly ISecretBackend WeakNotAllowedBackend = new UnavailableSecretBackend(
        saveStatus: JmcSecretWriteStatus.WeakProtectionNotAllowed);

    public static ISecretBackend Select(bool allowWeakFileProtection)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsBackend.Value;
        }

        return allowWeakFileProtection ? WeakFileBackend.Value : WeakNotAllowedBackend;
    }

    public static JmcSecretProtectionLevel GetProtectionLevel(bool allowWeakFileProtection)
    {
        return Select(allowWeakFileProtection).ProtectionLevel;
    }
}
