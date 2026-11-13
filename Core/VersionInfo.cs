// 文件用途：定义 JmcModLib 自身的名称、版本号与程序集版本读取辅助方法。
using System.Reflection;

namespace JmcModLib.Core;

public static class VersionInfo
{
    public const string Name = "JmcModLib";
    /// <summary>当前发布版本号，用于日志标识与版本信息展示。</summary>
    public const string Version = "1.10.0";

    public static string Tag => $"[{Name} v{Version}]";

    public static string GetName(Assembly? assembly = null)
    {
        assembly ??= typeof(VersionInfo).Assembly;
        return assembly == typeof(VersionInfo).Assembly
            ? Name
            : assembly.GetName().Name ?? Name;
    }

    public static string GetVersion(Assembly? assembly = null)
    {
        assembly ??= typeof(VersionInfo).Assembly;
        if (assembly == typeof(VersionInfo).Assembly)
        {
            return Version;
        }

        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
    }

    public static string GetTag(Assembly? assembly = null)
    {
        return $"[{GetName(assembly)} v{GetVersion(assembly)}]";
    }
}
