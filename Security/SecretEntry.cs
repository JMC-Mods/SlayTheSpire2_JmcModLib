using JmcModLib.Config;
using JmcModLib.Config.Entry;
using JmcModLib.Config.Storage;
using System.Reflection;

namespace JmcModLib.Security;

internal sealed class SecretEntry : ConfigEntry
{
    private SecretEntry(
        Assembly assembly,
        string storageKey,
        string group,
        string displayName,
        JmcSecretSlot slot,
        string? setButtonText,
        string? clearButtonText,
        string? setButtonTextKey,
        string? clearButtonTextKey,
        ConfigAttribute attribute)
        : base(assembly, storageKey, group, displayName, attribute, null)
    {
        Slot = slot;
        SetButtonText = setButtonText;
        ClearButtonText = clearButtonText;
        SetButtonTextKey = setButtonTextKey;
        ClearButtonTextKey = clearButtonTextKey;
    }

    public JmcSecretSlot Slot { get; }

    public string? SetButtonText { get; }

    public string? ClearButtonText { get; }

    public string? SetButtonTextKey { get; }

    public string? ClearButtonTextKey { get; }

    public override Type ValueType => typeof(void);

    public override object? DefaultValue => null;

    public static SecretEntry Create(
        Assembly assembly,
        string key,
        JmcSecretSlot slot,
        JmcSecretOptions options,
        string fallbackDisplayName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(options);

        string group = string.IsNullOrWhiteSpace(options.Group)
            ? ConfigAttribute.DefaultGroup
            : options.Group.Trim();
        string displayName = string.IsNullOrWhiteSpace(options.DisplayName)
            ? fallbackDisplayName
            : options.DisplayName.Trim();
        var descriptor = new ConfigAttribute(displayName, group: group)
        {
            Key = key.Trim(),
            Description = options.Description,
            LocTable = options.LocTable,
            DisplayNameKey = options.DisplayNameKey,
            DescriptionKey = options.DescriptionKey,
            GroupKey = options.GroupKey,
            Order = options.Order
        };

        var registration = new SecretRegistration(
            assembly,
            key,
            group,
            options.ScopeProvider,
            options.AllowWeakFileProtection);
        slot.Bind(registration);

        return new SecretEntry(
            assembly,
            key.Trim(),
            group,
            displayName,
            slot,
            options.SetButtonText,
            options.ClearButtonText,
            options.SetButtonTextKey,
            options.ClearButtonTextKey,
            descriptor);
    }

    public bool TrySave(string value, out JmcSecretWriteStatus status)
    {
        bool ok = Slot.TrySave(value, out status);
        if (ok)
        {
            RaiseValueChanged(null);
        }

        return ok;
    }

    public bool TryDelete(out JmcSecretWriteStatus status)
    {
        bool ok = Slot.TryDelete(out status);
        if (ok)
        {
            RaiseValueChanged(null);
        }

        return ok;
    }

    public bool Exists()
    {
        return Slot.Exists();
    }

    public override object? GetValue()
    {
        return null;
    }

    public override void SetValue(object? value)
    {
    }

    public override bool Reset()
    {
        return false;
    }

    internal override void SyncFromStorage(IConfigStorage storage)
    {
    }

    internal override void SyncFromSource(IConfigStorage storage)
    {
    }
}
