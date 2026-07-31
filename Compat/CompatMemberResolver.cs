using JmcModLib.Reflection;

namespace JmcModLib.Compat;

internal static class CompatMemberResolver
{
    // 0.110 集中出现了成员改名、可见性收紧和字段改方法等变化；候选解析统一放在这里，
    // 实际读写仍使用 JML 的 MemberAccessor / MethodAccessor，避免各兼容项重复原生反射代码。
    internal static MemberAccessor? FindReadableMember(
        Type type,
        bool isStatic,
        params string[] memberNames)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            foreach (string memberName in memberNames)
            {
                try
                {
                    MemberAccessor accessor = MemberAccessor.Get(current, memberName);
                    if (accessor.CanRead && accessor.IsStatic == isStatic)
                    {
                        return accessor;
                    }
                }
                catch (MissingMemberException)
                {
                    // 不同游戏版本使用不同成员名称，继续尝试下一个候选。
                }
            }
        }

        return null;
    }

    internal static MemberAccessor? FindWritableMember(
        Type type,
        bool isStatic,
        params string[] memberNames)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            foreach (string memberName in memberNames)
            {
                try
                {
                    MemberAccessor accessor = MemberAccessor.Get(current, memberName);
                    if (accessor.CanWrite && accessor.IsStatic == isStatic)
                    {
                        return accessor;
                    }
                }
                catch (MissingMemberException)
                {
                    // 不同游戏版本使用不同成员名称，继续尝试下一个候选。
                }
            }
        }

        return null;
    }

    internal static MethodAccessor? FindMethod(
        Type type,
        bool isStatic,
        string methodName,
        params Type[] parameterTypes)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            try
            {
                MethodAccessor accessor = MethodAccessor.Get(current, methodName, parameterTypes);
                if (accessor.IsStatic == isStatic)
                {
                    return accessor;
                }
            }
            catch (MissingMethodException)
            {
                // 当前游戏版本没有这个候选方法。
            }
        }

        return null;
    }
}
