using JmcModLib.Reflection;

namespace JmcModLib.Compat;

internal static class CompatMemberResolver
{
    // 兼容层只在这里枚举游戏 DLL 的候选成员，实际读取仍统一使用 JML 的反射访问器。
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
