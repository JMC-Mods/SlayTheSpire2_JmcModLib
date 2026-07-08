using JmcModLib.Core.AttributeRouter;
using JmcModLib.Reflection;
using System.Reflection;

namespace JmcModLib.Persistence.AttributeRouting;

internal sealed class PersistenceAttributeHandler(PersistenceScope scope) : IAttributeHandler
{
    public Action<Assembly, IReadOnlyList<ReflectionAccessorBase>>? Unregister => null;

    public void Handle(Assembly assembly, ReflectionAccessorBase accessor, Attribute attribute)
    {
        if (accessor is not MemberAccessor member)
        {
            return;
        }

        if (!TryCreateDescriptor(attribute, out PersistenceAttributeDescriptor descriptor))
        {
            return;
        }

        try
        {
            JmcPersistenceManager.RegisterFromAttribute(assembly, member, scope, descriptor);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"注册 Persistence 数据失败：{member.DeclaringType.FullName}.{member.Name}", ex, assembly);
        }
    }

    private static bool TryCreateDescriptor(Attribute attribute, out PersistenceAttributeDescriptor descriptor)
    {
        descriptor = default;
        switch (attribute)
        {
            case JmcLocalPreferenceAttribute localPreference:
                descriptor = new PersistenceAttributeDescriptor(
                    localPreference.Key,
                    localPreference.SchemaVersion,
                    localPreference.WritePolicy);
                return true;

            case JmcGlobalDataAttribute global:
                descriptor = new PersistenceAttributeDescriptor(global.Key, global.SchemaVersion, global.WritePolicy);
                return true;

            case JmcProfileDataAttribute profile:
                descriptor = new PersistenceAttributeDescriptor(profile.Key, profile.SchemaVersion, profile.WritePolicy);
                return true;

            case JmcRunDataAttribute run:
                descriptor = new PersistenceAttributeDescriptor(run.Key, run.SchemaVersion, run.WritePolicy);
                return true;

            default:
                return false;
        }
    }
}

internal readonly record struct PersistenceAttributeDescriptor(
    string Key,
    int SchemaVersion,
    JmcDataWritePolicy WritePolicy);
