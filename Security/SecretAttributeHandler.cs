using JmcModLib.Config;
using JmcModLib.Core.AttributeRouter;
using JmcModLib.Reflection;
using System.Reflection;

namespace JmcModLib.Security;

internal sealed class SecretAttributeHandler : IAttributeHandler
{
    public Action<Assembly, IReadOnlyList<ReflectionAccessorBase>>? Unregister => null;

    public void Handle(Assembly assembly, ReflectionAccessorBase accessor, Attribute attribute)
    {
        if (accessor is not MemberAccessor member || attribute is not SecretAttribute secretAttribute)
        {
            return;
        }

        try
        {
            if (!TryBuildSlot(member, out JmcSecretSlot? slot) || slot == null)
            {
                return;
            }

            JmcSecretOptions options = CreateOptions(member, secretAttribute, assembly);
            ConfigManager.RegisterSecret(slot, secretAttribute.Key, options, assembly, member.Name, member.DeclaringType, member.Name);
            ModLogger.Trace($"Registered secret entry {secretAttribute.Key}", assembly);
        }
        catch (Exception ex)
        {
            ModLogger.Error(
                $"注册 Secret 失败：{member.DeclaringType.FullName}.{member.Name}",
                ex,
                assembly);
        }
    }

    private static bool TryBuildSlot(MemberAccessor member, out JmcSecretSlot? slot)
    {
        slot = null;
        if (!member.IsStatic)
        {
            ModLogger.Error($"Secret 只能声明在静态字段或属性上：{member.DeclaringType.FullName}.{member.Name}");
            return false;
        }

        if (member.ValueType != typeof(JmcSecretSlot))
        {
            ModLogger.Error($"Secret 成员类型必须是 {typeof(JmcSecretSlot).FullName}：{member.DeclaringType.FullName}.{member.Name}");
            return false;
        }

        if (!member.CanRead)
        {
            ModLogger.Error($"Secret 成员必须可读：{member.DeclaringType.FullName}.{member.Name}");
            return false;
        }

        slot = member.GetValue<JmcSecretSlot>();
        if (slot != null)
        {
            return true;
        }

        if (!member.CanWrite)
        {
            ModLogger.Error($"Secret 成员为空且不可写，请初始化为 new JmcSecretSlot()：{member.DeclaringType.FullName}.{member.Name}");
            return false;
        }

        slot = new JmcSecretSlot();
        member.SetValue(slot);
        return true;
    }

    private static JmcSecretOptions CreateOptions(MemberAccessor member, SecretAttribute attribute, Assembly assembly)
    {
        return new JmcSecretOptions
        {
            Group = attribute.Group,
            LocTable = attribute.LocTable,
            DisplayName = string.IsNullOrWhiteSpace(attribute.DisplayName) ? member.Name : attribute.DisplayName,
            Description = attribute.Description,
            DisplayNameKey = attribute.DisplayNameKey,
            DescriptionKey = attribute.DescriptionKey,
            SetButtonTextKey = attribute.SetButtonTextKey,
            ClearButtonTextKey = attribute.ClearButtonTextKey,
            GroupKey = attribute.GroupKey,
            ScopeProvider = BuildScopeProvider(member, attribute, assembly),
            AllowWeakFileProtection = attribute.AllowWeakFileProtection,
            Order = attribute.Order
        };
    }

    private static Func<string>? BuildScopeProvider(MemberAccessor member, SecretAttribute attribute, Assembly assembly)
    {
        if (string.IsNullOrWhiteSpace(attribute.ScopeProvider))
        {
            return null;
        }

        string providerName = attribute.ScopeProvider.Trim();
        Type declaringType = member.DeclaringType;
        MethodAccessor? method = MethodAccessor
            .GetAll(declaringType)
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name, providerName, StringComparison.Ordinal)
                && candidate.IsStatic
                && candidate.MemberInfo.GetParameters().Length == 0);
        if (method != null)
        {
            if (method.MemberInfo.ReturnType != typeof(string))
            {
                ModLogger.Error($"Secret ScopeProvider 方法必须返回 string：{declaringType.FullName}.{providerName}", assembly);
                return null;
            }

            return () => method.InvokeStatic<string>() ?? string.Empty;
        }

        MemberAccessor? property = MemberAccessor
            .GetAll(declaringType)
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name, providerName, StringComparison.Ordinal)
                && candidate.MemberInfo is PropertyInfo
                && candidate.IsStatic);
        if (property != null)
        {
            if (property.ValueType != typeof(string))
            {
                ModLogger.Error($"Secret ScopeProvider 属性必须返回 string：{declaringType.FullName}.{providerName}", assembly);
                return null;
            }

            if (!property.CanRead)
            {
                ModLogger.Error($"Secret ScopeProvider 属性必须可读：{declaringType.FullName}.{providerName}", assembly);
                return null;
            }

            return () => property.GetValue<string>() ?? string.Empty;
        }

        ModLogger.Error($"找不到 Secret ScopeProvider：{declaringType.FullName}.{providerName}", assembly);
        return null;
    }
}
