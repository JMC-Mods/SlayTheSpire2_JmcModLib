using JmcModLib.Reflection;

namespace JmcModLib.Persistence.Entries;

internal sealed class PersistenceMemberEntry<T> : PersistenceEntry
{
    private readonly MemberAccessor member;
    private readonly Func<T> getter;
    private readonly Action<T> setter;

    public PersistenceMemberEntry(JmcDataRegistration registration, MemberAccessor member)
        : base(registration, typeof(T), ReadInitialValue(member))
    {
        this.member = member ?? throw new ArgumentNullException(nameof(member));
        getter = member.TypedGetter is Func<T> typedGetter
            ? typedGetter
            : () => (T)member.GetValue(null)!;
        setter = member.TypedSetter is Action<T> typedSetter
            ? typedSetter
            : value => member.SetValue(null, value);
    }

    protected override object? GetCurrentValue()
    {
        return getter();
    }

    protected override void ApplyValue(object? value)
    {
        setter(value is null ? default! : (T)value);
    }

    private static object? ReadInitialValue(MemberAccessor member)
    {
        try
        {
            return member.GetValue(null);
        }
        catch
        {
            return default(T);
        }
    }
}
