// 文件用途：作为子 MOD 的同名入口 DLL，按 STS2 版本分派并加载真正的 Runtime DLL。
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Modding;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace JmcModLib.Dispatch.Bootstrap;

[ModInitializer(nameof(Initialize))]
internal static class DispatchBootstrap
{
    private const string DescriptorSuffix = ".dispatch.json";

    public static void Initialize()
    {
        Assembly bootstrapAssembly = Assembly.GetExecutingAssembly();
        string modName = bootstrapAssembly.GetName().Name ?? "UnknownMod";
        string modDirectory = ResolveModDirectory(bootstrapAssembly);
        string descriptorPath = Path.Combine(modDirectory, modName + DescriptorSuffix);

        GameVersionInfo gameVersion = GetCurrentGameVersion();
        DispatchDescriptor descriptor = DispatchDescriptor.Load(descriptorPath, modName);
        DispatchEntry entry = descriptor.SelectEntry(gameVersion);

        BootstrapDependencyResolver.Install(modDirectory, descriptor, entry);

        Assembly runtimeAssembly = BootstrapDependencyResolver.LoadRuntimeAssembly(entry.RuntimeAssembly);
        InvokeRuntimeInitializer(runtimeAssembly, descriptor);

        BootstrapLog.Info($"版本分派完成：{entry.Id} -> {entry.RuntimeAssembly}");
    }

    private static GameVersionInfo GetCurrentGameVersion()
    {
        try
        {
            ReleaseInfoManager manager = ReleaseInfoManager.Instance;
            return new GameVersionInfo(manager.ReleaseInfo?.Version, manager.SemVer);
        }
        catch (Exception ex)
        {
            BootstrapLog.Warn($"读取 STS2 版本失败，将只匹配无版本范围的分派项：{ex.Message}");
            return new GameVersionInfo(null, null);
        }
    }

    private static void InvokeRuntimeInitializer(Assembly runtimeAssembly, DispatchDescriptor descriptor)
    {
        Type runtimeType = runtimeAssembly.GetType(descriptor.InitializerType, throwOnError: true)
            ?? throw new TypeLoadException($"找不到 Runtime 初始化类型：{descriptor.InitializerType}");

        MethodInfo method = runtimeType.GetMethod(
                descriptor.InitializerMethod,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(descriptor.InitializerType, descriptor.InitializerMethod);

        try
        {
            method.Invoke(null, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    private static string ResolveModDirectory(Assembly assembly)
    {
        string? location = assembly.Location;
        if (!string.IsNullOrWhiteSpace(location))
        {
            string? directory = Path.GetDirectoryName(location);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return directory;
            }
        }

        return AppContext.BaseDirectory;
    }
}

internal readonly record struct GameVersionInfo(string? RawVersion, SemanticVersion? SemVer);
