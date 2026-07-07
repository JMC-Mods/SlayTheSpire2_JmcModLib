using JmcModLib.Persistence.Storage;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace JmcModLib.Persistence.Entries;

internal abstract class PersistenceEntry
{
    private readonly object? defaultValue;

    protected PersistenceEntry(JmcDataRegistration registration, Type valueType, object? initialValue)
    {
        Registration = registration ?? throw new ArgumentNullException(nameof(registration));
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        defaultValue = PersistenceJson.CloneValue(initialValue, valueType);
    }

    public JmcDataRegistration Registration { get; }

    public Assembly Assembly => Registration.Assembly;

    public PersistenceScope Scope => Registration.Scope;

    public string Key => Registration.Key;

    public string StorageKey => Registration.StorageKey;

    public string ModId => Registration.ModId;

    public int SchemaVersion => Registration.SchemaVersion;

    public JmcDataWritePolicy WritePolicy => Registration.WritePolicy;

    public Type ValueType { get; }

    public bool IsDirty { get; private set; }

    protected JToken? LastSavedToken { get; private set; }

    public void InitializeFromToken(JToken? token)
    {
        if (token == null || token.Type is JTokenType.Undefined)
        {
            ResetToDefault();
            LastSavedToken = null;
            IsDirty = WritePolicy == JmcDataWritePolicy.Always;
            return;
        }

        try
        {
            object? value = PersistenceJson.ToObject(token, ValueType);
            ApplyValue(value);
            LastSavedToken = token.DeepClone();
            IsDirty = WritePolicy == JmcDataWritePolicy.Always;
        }
        catch (Exception ex)
        {
            ModLogger.Error($"读取 Persistence 数据失败：{Registration.SourceDescription} ({Key})", ex, Assembly);
            ResetToDefault();
            LastSavedToken = null;
            IsDirty = WritePolicy == JmcDataWritePolicy.Always;
        }
    }

    public PersistenceDocumentEntry? CaptureDocumentEntry()
    {
        JToken token;
        try
        {
            token = PersistenceJson.ToToken(GetCurrentValue());
        }
        catch (Exception ex)
        {
            ModLogger.Error($"序列化 Persistence 数据失败：{Registration.SourceDescription} ({Key})", ex, Assembly);
            return null;
        }

        bool changed = WritePolicy == JmcDataWritePolicy.Always
            || IsDirty
            || LastSavedToken == null
            || !JToken.DeepEquals(token, LastSavedToken);
        if (!changed)
        {
            return null;
        }

        LastSavedToken = token.DeepClone();
        IsDirty = false;
        return new PersistenceDocumentEntry
        {
            SchemaVersion = SchemaVersion,
            Value = token,
        };
    }

    public void MarkDirty()
    {
        IsDirty = true;
    }

    public void ResetToDefault()
    {
        object? cloned = PersistenceJson.CloneValue(defaultValue, ValueType);
        ApplyValue(cloned);
    }

    public virtual void Detach()
    {
    }

    protected abstract object? GetCurrentValue();

    protected abstract void ApplyValue(object? value);
}
