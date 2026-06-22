// 文件用途：为被分派选中的 Runtime DLL 安装最小范围的程序集解析。
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;

namespace JmcModLib.Dispatch.Bootstrap;

internal static class BootstrapDependencyResolver
{
    private static readonly ConcurrentDictionary<string, string> AssemblyPaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> ProbeDirectories = [];
    private static readonly object ConfigureLock = new();

    [ThreadStatic]
    private static HashSet<string>? resolvingNames;

    private static AssemblyLoadContext? loadContext;
    private static int installed;

    public static void Install(string modDirectory, DispatchDescriptor descriptor, DispatchEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modDirectory);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(entry);

        lock (ConfigureLock)
        {
            loadContext ??= AssemblyLoadContext.GetLoadContext(typeof(BootstrapDependencyResolver).Assembly)
                ?? AssemblyLoadContext.Default;

            AddProbeDirectories(modDirectory, entry.ProbeDirectories);
            AddDependencyPath(modDirectory, entry.RuntimeAssembly);

            foreach (string dependency in entry.Dependencies)
            {
                AddDependencyPath(modDirectory, dependency);
            }

            if (descriptor.ProbeAllDlls || entry.EffectiveProbeAllDlls)
            {
                AddAllDllsFromProbeDirectories();
            }

            if (Interlocked.Exchange(ref installed, 1) == 0)
            {
                loadContext.Resolving += ResolveFromLoadContext;
                AppDomain.CurrentDomain.AssemblyResolve += ResolveFromAppDomain;
                BootstrapLog.Info("分派依赖解析器已安装。");
            }
        }
    }

    public static Assembly LoadRuntimeAssembly(string runtimeAssembly)
    {
        string? runtimePath = FindDependencyPath(runtimeAssembly);
        if (runtimePath == null)
        {
            throw new FileNotFoundException($"找不到 Runtime DLL：{runtimeAssembly}");
        }

        AssemblyName runtimeName = AssemblyName.GetAssemblyName(runtimePath);
        Assembly? loaded = FindLoadedAssembly(runtimeName);
        if (loaded != null)
        {
            string loadedPath = loaded.Location;
            if (!PathEquals(loadedPath, runtimePath))
            {
                throw new InvalidOperationException(
                    $"Runtime 程序集标识 {runtimeName.Name} 已被 {loadedPath} 占用。"
                    + "请将 Runtime 项目的 AssemblyName 设为不同于入口 DLL 的名称，例如 MyMod.Runtime。");
            }

            BootstrapLog.Info($"Runtime 已加载，复用程序集：{loaded.FullName}");
            return loaded;
        }

        Assembly assembly = (loadContext ?? AssemblyLoadContext.Default).LoadFromAssemblyPath(runtimePath);
        BootstrapLog.Info($"已加载 Runtime：{runtimePath}");
        return assembly;
    }

    private static Assembly? ResolveFromLoadContext(AssemblyLoadContext context, AssemblyName requestedAssembly)
    {
        return ResolveAssembly(context, requestedAssembly);
    }

    private static Assembly? ResolveFromAppDomain(object? sender, ResolveEventArgs args)
    {
        return ResolveAssembly(loadContext ?? AssemblyLoadContext.Default, new AssemblyName(args.Name));
    }

    private static Assembly? ResolveAssembly(AssemblyLoadContext context, AssemblyName requestedAssembly)
    {
        string simpleName = requestedAssembly.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(simpleName))
        {
            return null;
        }

        resolvingNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!resolvingNames.Add(simpleName))
        {
            return null;
        }

        try
        {
            Assembly? loaded = FindLoadedAssembly(requestedAssembly);
            if (loaded != null)
            {
                return loaded;
            }

            string? assemblyPath = FindDependencyPath(simpleName);
            if (assemblyPath == null)
            {
                return null;
            }

            AssemblyName candidateName = AssemblyName.GetAssemblyName(assemblyPath);
            if (!IsCompatible(candidateName, requestedAssembly))
            {
                BootstrapLog.Warn($"跳过不兼容依赖：请求 {requestedAssembly.FullName}，找到 {candidateName.FullName}，路径 {assemblyPath}");
                return null;
            }

            Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);
            BootstrapLog.Info($"已解析依赖：{requestedAssembly.Name} -> {assemblyPath}");
            return assembly;
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException or FileLoadException)
        {
            BootstrapLog.Error($"解析依赖失败：{requestedAssembly.FullName}\n{ex}");
            return null;
        }
        finally
        {
            _ = resolvingNames.Remove(simpleName);
        }
    }

    private static void AddProbeDirectories(string modDirectory, IEnumerable<string> probeDirectories)
    {
        foreach (string probeDirectory in probeDirectories)
        {
            string fullPath = ResolvePath(modDirectory, probeDirectory);
            if (Directory.Exists(fullPath) && !ProbeDirectories.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                ProbeDirectories.Add(fullPath);
                BootstrapLog.Info($"添加依赖探测目录：{fullPath}");
            }
        }
    }

    private static void AddDependencyPath(string modDirectory, string dependency)
    {
        foreach (string candidate in EnumerateDependencyCandidates(modDirectory, dependency))
        {
            if (File.Exists(candidate))
            {
                AddAssemblyPath(candidate);
                return;
            }
        }

        BootstrapLog.Warn($"依赖文件不存在：{dependency}");
    }

    private static IEnumerable<string> EnumerateDependencyCandidates(string modDirectory, string dependency)
    {
        string normalizedDependency = dependency.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? dependency
            : $"{dependency}.dll";

        if (Path.IsPathRooted(normalizedDependency))
        {
            yield return normalizedDependency;
            yield break;
        }

        yield return Path.GetFullPath(Path.Combine(modDirectory, normalizedDependency));

        foreach (string probeDirectory in ProbeDirectories)
        {
            yield return Path.GetFullPath(Path.Combine(probeDirectory, normalizedDependency));
        }
    }

    private static void AddAllDllsFromProbeDirectories()
    {
        foreach (string probeDirectory in ProbeDirectories)
        {
            foreach (string assemblyPath in Directory.EnumerateFiles(probeDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                AddAssemblyPath(assemblyPath);
            }
        }
    }

    private static void AddAssemblyPath(string assemblyPath)
    {
        try
        {
            AssemblyName assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
            if (!string.IsNullOrWhiteSpace(assemblyName.Name))
            {
                AssemblyPaths[assemblyName.Name] = assemblyPath;
            }
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException)
        {
            BootstrapLog.Warn($"跳过无效依赖：{assemblyPath}，原因：{ex.Message}");
        }
    }

    private static string? FindDependencyPath(string assemblyNameOrPath)
    {
        string simpleName = NormalizeAssemblyName(assemblyNameOrPath);
        if (!string.IsNullOrWhiteSpace(simpleName) && AssemblyPaths.TryGetValue(simpleName, out string? mappedPath))
        {
            return mappedPath;
        }

        string fileName = assemblyNameOrPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(assemblyNameOrPath)
            : $"{simpleName}.dll";

        foreach (string probeDirectory in ProbeDirectories)
        {
            string candidate = Path.Combine(probeDirectory, fileName);
            if (File.Exists(candidate))
            {
                AddAssemblyPath(candidate);
                return candidate;
            }
        }

        return null;
    }

    private static Assembly? FindLoadedAssembly(AssemblyName requestedAssembly)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            AssemblyName loadedName = assembly.GetName();
            if (IsCompatible(loadedName, requestedAssembly))
            {
                return assembly;
            }
        }

        return null;
    }

    private static bool IsCompatible(AssemblyName candidate, AssemblyName requested)
    {
        if (!string.Equals(candidate.Name, requested.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[]? requestedToken = requested.GetPublicKeyToken();
        byte[]? candidateToken = candidate.GetPublicKeyToken();
        if (requestedToken is { Length: > 0 }
            && (candidateToken is not { Length: > 0 } || !requestedToken.SequenceEqual(candidateToken)))
        {
            return false;
        }

        if (requested.Version == null || candidate.Version == null)
        {
            return true;
        }

        return candidate.Version.CompareTo(requested.Version) >= 0;
    }

    private static string ResolvePath(string baseDirectory, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path));
    }

    private static string NormalizeAssemblyName(string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return string.Empty;
        }

        string trimmed = assemblyName.Trim();
        if (trimmed.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileNameWithoutExtension(trimmed);
        }

        return trimmed.Contains(',')
            ? new AssemblyName(trimmed).Name ?? string.Empty
            : trimmed;
    }

    private static bool PathEquals(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }
}
