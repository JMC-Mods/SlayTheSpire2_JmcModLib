// 文件用途：读取并规范化子 MOD 的版本分派配置。
using MegaCrit.Sts2.Core.Debug;
using System.Text.Json;

namespace JmcModLib.Dispatch.Bootstrap;

internal sealed class DispatchDescriptor
{
    public string InitializerType { get; set; } = string.Empty;

    public string InitializerMethod { get; set; } = "Initialize";

    public List<DispatchEntry> Entries { get; set; } = [];

    public string? FallbackRuntimeAssembly { get; set; }

    public List<string> FallbackProbeDirectories { get; set; } = [];

    public List<string> FallbackDependencies { get; set; } = [];

    public bool ProbeAllDlls { get; set; }

    public static DispatchDescriptor Load(string descriptorPath, string modName)
    {
        if (!File.Exists(descriptorPath))
        {
            throw new FileNotFoundException($"找不到分派配置文件：{descriptorPath}");
        }

        try
        {
            string json = File.ReadAllText(descriptorPath);
            DispatchDescriptor descriptor = JsonSerializer.Deserialize<DispatchDescriptor>(
                    json,
                    new JsonSerializerOptions
                    {
                        AllowTrailingCommas = true,
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    })
                ?? new DispatchDescriptor();

            descriptor.Normalize(modName);
            BootstrapLog.Info($"已读取分派配置：{descriptorPath}");
            return descriptor;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"分派配置 JSON 格式无效：{descriptorPath}", ex);
        }
    }

    public DispatchEntry SelectEntry(GameVersionInfo gameVersion)
    {
        BootstrapLog.Info($"当前 STS2 版本：{gameVersion.RawVersion ?? "<未知>"}");

        foreach (DispatchEntry entry in Entries)
        {
            if (entry.IsMatch(gameVersion))
            {
                BootstrapLog.Info($"命中分派项：{entry.Id}");
                return entry;
            }
        }

        throw new InvalidOperationException(
            $"没有找到匹配当前 STS2 版本 {gameVersion.RawVersion ?? "<未知>"} 的 Runtime 分派项。");
    }

    public static List<string> NormalizeList(IEnumerable<string>? values)
    {
        if (values == null)
        {
            return [];
        }

        List<string> result = [];
        foreach (string value in values)
        {
            foreach (string item in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(item) && !result.Contains(item, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(item);
                }
            }
        }

        return result;
    }

    private void Normalize(string modName)
    {
        InitializerType = NormalizeText(InitializerType, $"{modName}.MainFile");
        InitializerMethod = NormalizeText(InitializerMethod, "Initialize");
        FallbackProbeDirectories = NormalizeList(FallbackProbeDirectories);
        FallbackDependencies = NormalizeList(FallbackDependencies);

        foreach (DispatchEntry entry in Entries)
        {
            entry.Normalize();
        }

        if (!string.IsNullOrWhiteSpace(FallbackRuntimeAssembly))
        {
            var fallback = new DispatchEntry
            {
                Id = "fallback",
                RuntimeAssembly = FallbackRuntimeAssembly!,
                ProbeDirectories = FallbackProbeDirectories,
                Dependencies = FallbackDependencies,
                ProbeAllDlls = ProbeAllDlls
            };
            fallback.Normalize();
            Entries.Add(fallback);
        }

        if (Entries.Count == 0)
        {
            throw new InvalidOperationException("分派配置至少需要一个 entries 项或 fallbackRuntimeAssembly。");
        }
    }

    private static string NormalizeText(string? text, string fallback)
    {
        return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
    }
}

internal sealed class DispatchEntry
{
    public string Id { get; set; } = string.Empty;

    public string? MinGameVersion { get; set; }

    public string? MaxGameVersionExclusive { get; set; }

    public string RuntimeAssembly { get; set; } = string.Empty;

    public List<string> ProbeDirectories { get; set; } = [];

    public List<string> Dependencies { get; set; } = [];

    public bool? ProbeAllDlls { get; set; }

    private SemanticVersion? minVersion;

    private SemanticVersion? maxVersionExclusive;

    public bool EffectiveProbeAllDlls => ProbeAllDlls == true;

    public void Normalize()
    {
        Id = string.IsNullOrWhiteSpace(Id) ? RuntimeAssembly : Id.Trim();
        RuntimeAssembly = NormalizeText(RuntimeAssembly, string.Empty);
        if (string.IsNullOrWhiteSpace(RuntimeAssembly))
        {
            throw new InvalidOperationException($"分派项 {Id} 缺少 runtimeAssembly。");
        }

        MinGameVersion = NormalizeOptionalText(MinGameVersion);
        MaxGameVersionExclusive = NormalizeOptionalText(MaxGameVersionExclusive);
        ProbeDirectories = DispatchDescriptor.NormalizeList(ProbeDirectories);
        Dependencies = DispatchDescriptor.NormalizeList(Dependencies);

        if (ProbeDirectories.Count == 0)
        {
            string? directory = Path.GetDirectoryName(RuntimeAssembly);
            ProbeDirectories.Add(string.IsNullOrWhiteSpace(directory) ? "." : directory);
        }

        minVersion = ParseOptionalVersion(MinGameVersion, nameof(MinGameVersion));
        maxVersionExclusive = ParseOptionalVersion(MaxGameVersionExclusive, nameof(MaxGameVersionExclusive));
    }

    public bool IsMatch(GameVersionInfo gameVersion)
    {
        if (minVersion == null && maxVersionExclusive == null)
        {
            return true;
        }

        if (gameVersion.SemVer == null)
        {
            return false;
        }

        if (minVersion != null && gameVersion.SemVer.CompareTo(minVersion) < 0)
        {
            return false;
        }

        return maxVersionExclusive == null || gameVersion.SemVer.CompareTo(maxVersionExclusive) < 0;
    }

    private static SemanticVersion? ParseOptionalVersion(string? version, string memberName)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        if (SemanticVersion.TryFromString(version, out SemanticVersion? parsed) && parsed != null)
        {
            return parsed;
        }

        throw new InvalidOperationException($"{memberName} 不是有效的语义化版本：{version}");
    }

    private static string NormalizeText(string? text, string fallback)
    {
        return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
    }

    private static string? NormalizeOptionalText(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
