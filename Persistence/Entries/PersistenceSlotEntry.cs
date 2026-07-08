namespace JmcModLib.Persistence.Entries;

internal sealed class PersistenceSlotEntry<T> : PersistenceEntry, IJmcRunDataSlotBinding<T>
{
    private readonly JmcDataSlot<T>? dataSlot;
    private readonly JmcRunDataSlot<T>? runDataSlot;
    private T? value;

    public PersistenceSlotEntry(JmcDataRegistration registration, JmcDataSlot<T> slot)
        : base(registration, typeof(T), slot.GetLocalValue())
    {
        dataSlot = slot ?? throw new ArgumentNullException(nameof(slot));
        value = slot.GetLocalValue();
        dataSlot.Bind(this);
    }

    public PersistenceSlotEntry(JmcDataRegistration registration, JmcRunDataSlot<T> slot)
        : base(registration, typeof(T), slot.GetLocalValue())
    {
        runDataSlot = slot ?? throw new ArgumentNullException(nameof(slot));
        value = slot.GetLocalValue();
        runDataSlot.Bind(this);
    }

    public bool CanAccess => Scope switch
    {
        PersistenceScope.Run => JmcModLib.Persistence.Run.RunPersistenceManager.CanAccessRunData,
        PersistenceScope.ClientRun => JmcModLib.Persistence.Run.RunPersistenceManager.HasClientRunContext,
        _ => true,
    };

    public T GetValue()
    {
        return value!;
    }

    public JmcDataWriteResult SetValue(T nextValue)
    {
        if (Scope is PersistenceScope.Run or PersistenceScope.ClientRun && !CanAccess)
        {
            LogRunContextUnavailable();
            return JmcDataWriteResult.Failed("当前没有可写入的 run 上下文。");
        }

        value = nextValue;
        dataSlot?.SetLocalValue(nextValue);
        runDataSlot?.SetLocalValue(nextValue);
        MarkDirty();

        if (Scope == PersistenceScope.Run)
        {
            JmcModLib.Persistence.Run.RunPersistenceManager.MarkDirty();
        }
        else if (Scope == PersistenceScope.ClientRun)
        {
            JmcPersistenceManager.FlushClientRunData(Assembly);
        }
        else if (Scope == PersistenceScope.LocalPreference)
        {
            JmcPersistenceManager.FlushLocalPreferences(Assembly);
        }

        return JmcDataWriteResult.Succeeded();
    }

    public void LogRunContextUnavailable()
    {
        ModLogger.Warn($"当前没有可写入的 run 上下文，Persistence 写入已跳过：{Registration.SourceDescription}", Assembly);
    }

    public override void Detach()
    {
        dataSlot?.Unbind(this);
        runDataSlot?.Unbind(this);
    }

    protected override object? GetCurrentValue()
    {
        return value;
    }

    protected override void ApplyValue(object? nextValue)
    {
        value = nextValue is null ? default : (T)nextValue;
        dataSlot?.SetLocalValue(value!);
        runDataSlot?.SetLocalValue(value!);
    }
}
