using Microsoft.Win32;
using Steamworks;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JmcModLib.Input;

internal readonly record struct SteamInputConfigurationEntry(string ControllerType, string Path);

internal static partial class SteamInputConfigurationProvider
{
    private const uint Sts2AppId = 2868840;
    private const string ConfigSetPrefix = "configset_";
    private const string VdfExtension = ".vdf";

    public static IReadOnlyList<SteamInputConfigurationEntry> PrepareConfigurations(
        string outputDirectory,
        IReadOnlyList<JmcInputActionDescriptor> actions,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> localization)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(localization);

        Directory.CreateDirectory(outputDirectory);
        string accountId = SteamUser.GetSteamID().GetAccountID().m_AccountID.ToString(CultureInfo.InvariantCulture);
        string[] appConfigIds = ResolveAppConfigIds();
        var entries = new List<SteamInputConfigurationEntry>();
        var usedControllerTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (string steamRoot in EnumerateSteamRoots())
        {
            string configRoot = Path.Combine(
                steamRoot,
                "steamapps",
                "common",
                "Steam Controller Configs",
                accountId,
                "config");
            if (!Directory.Exists(configRoot))
            {
                continue;
            }

            foreach (string configSetPath in Directory.EnumerateFiles(configRoot, $"{ConfigSetPrefix}*{VdfExtension}"))
            {
                string fileName = Path.GetFileNameWithoutExtension(configSetPath);
                string controllerType = fileName[ConfigSetPrefix.Length..];
                if (!usedControllerTypes.Add(controllerType))
                {
                    continue;
                }

                if (!TryFindAutosaveConfiguration(configSetPath, configRoot, controllerType, appConfigIds, out string sourcePath))
                {
                    continue;
                }

                string outputFileName = $"{controllerType}{VdfExtension}";
                string outputPath = Path.Combine(outputDirectory, outputFileName);
                try
                {
                    string sourceText = File.ReadAllText(sourcePath, Encoding.UTF8);
                    string mergedText = SteamInputManifestMerger.MergeControllerConfiguration(sourceText, actions, localization);
                    File.WriteAllText(
                        outputPath,
                        mergedText.ReplaceLineEndings("\r\n"),
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    entries.Add(new SteamInputConfigurationEntry(controllerType, outputFileName));
                    ModLogger.Debug($"JML Steam Input 使用当前 Steam 布局：{controllerType} => {sourcePath}");
                }
                catch (Exception ex)
                {
                    ModLogger.Warn($"复制 JML Steam Input 手柄布局失败：{sourcePath}。{ex.Message}");
                    _ = usedControllerTypes.Remove(controllerType);
                }
            }
        }

        return entries;
    }

    private static string[] ResolveAppConfigIds()
    {
        var ids = new List<string>();
        try
        {
            if (SteamApps.GetCurrentBetaName(out string betaName, 256)
                && !string.IsNullOrWhiteSpace(betaName))
            {
                ids.Add($"{Sts2AppId}-{betaName.TrimEnd('\0')}");
            }
        }
        catch (Exception ex)
        {
            ModLogger.Debug($"读取 Steam 当前分支失败，将仅使用默认 AppID 配置：{ex.Message}");
        }

        ids.Add(Sts2AppId.ToString(CultureInfo.InvariantCulture));
        return ids.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool TryFindAutosaveConfiguration(
        string configSetPath,
        string configRoot,
        string controllerType,
        IReadOnlyList<string> appConfigIds,
        out string sourcePath)
    {
        string text = File.ReadAllText(configSetPath, Encoding.UTF8);
        foreach (string appConfigId in appConfigIds)
        {
            Match match = AppConfigBlockRegex(appConfigId).Match(text);
            if (!match.Success
                || !match.Groups["body"].Value.Contains("\"autosave\"", StringComparison.Ordinal))
            {
                continue;
            }

            string candidate = Path.Combine(configRoot, appConfigId, $"{controllerType}{VdfExtension}");
            if (File.Exists(candidate))
            {
                sourcePath = candidate;
                return true;
            }

            ModLogger.Warn($"Steam 标记 {appConfigId}/{controllerType} 为 autosave，但布局文件不存在：{candidate}");
        }

        sourcePath = string.Empty;
        return false;
    }

    private static IEnumerable<string> EnumerateSteamRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? candidate in EnumerateSteamRootCandidates())
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            string normalized = candidate.Replace('/', Path.DirectorySeparatorChar).Trim();
            if (!Directory.Exists(normalized))
            {
                continue;
            }

            string fullPath = Path.GetFullPath(normalized);
            if (seen.Add(fullPath))
            {
                yield return fullPath;
            }
        }
    }

    private static IEnumerable<string?> EnumerateSteamRootCandidates()
    {
        yield return ReadRegistryString(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath");
        yield return ReadRegistryString(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam");

        foreach (string root in EnumerateAppInstallRoots())
        {
            yield return root;
        }
    }

    private static IEnumerable<string> EnumerateAppInstallRoots()
    {
        if (TryGetAppInstallDir(out string appInstallDir))
        {
            foreach (string root in WalkUpForSteamRoots(appInstallDir))
            {
                yield return root;
            }
        }

        foreach (string root in WalkUpForSteamRoots(AppContext.BaseDirectory))
        {
            yield return root;
        }
    }

    private static bool TryGetAppInstallDir(out string appInstallDir)
    {
        appInstallDir = string.Empty;
        try
        {
            if (SteamApps.GetAppInstallDir(new AppId_t(Sts2AppId), out string path, 1024) > 0
                && !string.IsNullOrWhiteSpace(path))
            {
                appInstallDir = path;
                return true;
            }
        }
        catch (Exception ex)
        {
            ModLogger.Debug($"读取 Steam 游戏安装目录失败：{ex.Message}");
        }

        return false;
    }

    private static IEnumerable<string> WalkUpForSteamRoots(string root)
    {
        DirectoryInfo? directory = Directory.Exists(root)
            ? new DirectoryInfo(root)
            : new DirectoryInfo(Path.GetDirectoryName(root) ?? root);

        while (directory != null)
        {
            if (directory.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase)
                && directory.Parent != null)
            {
                yield return directory.Parent.FullName;
            }

            if (Directory.Exists(Path.Combine(directory.FullName, "steamapps")))
            {
                yield return directory.FullName;
            }

            directory = directory.Parent;
        }
    }

    private static string? ReadRegistryString(string keyName, string valueName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return Registry.GetValue(keyName, valueName, null) as string;
        }
        catch (Exception ex)
        {
            ModLogger.Debug($"读取 Steam 注册表路径失败：{keyName}\\{valueName}。{ex.Message}");
            return null;
        }
    }

    private static Regex AppConfigBlockRegex(string appConfigId)
    {
        return new Regex(
            $"\"{Regex.Escape(appConfigId)}\"\\s*\\{{(?<body>.*?)\\}}",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }
}
