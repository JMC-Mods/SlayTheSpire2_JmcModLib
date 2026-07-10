using JmcModLib.Compat;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using System.Reflection;
using System.Text;

namespace JmcModLib.Multiplayer.Internal;

internal static class OptionalNetworkMismatchPresenter
{
    internal static bool TryCreatePopup(NetErrorInfo info, out NErrorPopup? popup)
    {
        popup = null;
        if (info.GetReason() != NetError.ModMismatch
            || !MultiplayerCompat.TryGetConnectionExtraInfo(
                info,
                out ConnectionFailureExtraInfo? extraInfo)
            || extraInfo == null)
        {
            return false;
        }

        List<string> missingOnHost = [.. extraInfo.missingModsOnHost ?? []];
        List<string> missingOnLocal = [.. extraInfo.missingModsOnLocal ?? []];
        List<OptionalNetworkFeatureIdentity> localOnly = ExtractFeatureIdentities(missingOnHost);
        List<OptionalNetworkFeatureIdentity> hostOnly = ExtractFeatureIdentities(missingOnLocal);
        if (localOnly.Count == 0 && hostOnly.Count == 0)
        {
            return false;
        }

        OptionalNetworkFeatureDescriptor[] localOnlyFeatures = localOnly.Select(ResolveFeature).ToArray();
        OptionalNetworkFeatureDescriptor[] hostOnlyFeatures = hostOnly.Select(ResolveFeature).ToArray();
        RemoveCompanionModEntries(missingOnHost, localOnlyFeatures);
        RemoveCompanionModEntries(missingOnLocal, hostOnlyFeatures);

        string body = OptionalNetworkMismatchText.BuildBody(
            localOnlyFeatures,
            hostOnlyFeatures,
            FormatRemainingMismatch(missingOnHost, missingOnLocal));
        popup = NErrorPopup.Create(OptionalNetworkMismatchText.Title(), body, showReportBugButton: false);
        return true;
    }

    private static List<OptionalNetworkFeatureIdentity> ExtractFeatureIdentities(List<string> entries)
    {
        var identities = new List<OptionalNetworkFeatureIdentity>();
        for (int index = entries.Count - 1; index >= 0; index--)
        {
            if (!OptionalNetworkFeatureIdentity.TryParse(entries[index], out OptionalNetworkFeatureIdentity identity))
            {
                continue;
            }

            identities.Add(identity);
            entries.RemoveAt(index);
        }

        identities.Reverse();
        return identities.Distinct().ToList();
    }

    private static OptionalNetworkFeatureDescriptor ResolveFeature(OptionalNetworkFeatureIdentity identity)
    {
        return OptionalNetworkFeatureManager.TryGetFeatureDescriptor(identity, out OptionalNetworkFeatureDescriptor descriptor)
            ? descriptor
            : new OptionalNetworkFeatureDescriptor(
                identity,
                identity.ModId,
                ModVersion: null,
                EffectiveEnabled: false);
    }

    private static void RemoveCompanionModEntries(
        List<string> entries,
        IEnumerable<OptionalNetworkFeatureDescriptor> features)
    {
        HashSet<string> exactModEntries = features
            .Where(static feature => !string.IsNullOrWhiteSpace(feature.ModVersion))
            .Select(static feature => $"{feature.Identity.ModId}-{feature.ModVersion}")
            .ToHashSet(StringComparer.Ordinal);
        entries.RemoveAll(exactModEntries.Contains);
    }

    private static string FormatRemainingMismatch(
        IReadOnlyCollection<string> missingOnHost,
        IReadOnlyCollection<string> missingOnLocal)
    {
        var builder = new StringBuilder();
        if (missingOnHost.Count > 0)
        {
            var text = new LocString(
                "main_menu_ui",
                "NETWORK_ERROR.MOD_MISMATCH.description.missingOnHost");
            text.Add("mods", string.Join(", ", missingOnHost));
            builder.AppendLine(text.GetFormattedText());
        }

        if (missingOnLocal.Count > 0)
        {
            var text = new LocString(
                "main_menu_ui",
                "NETWORK_ERROR.MOD_MISMATCH.description.missingOnLocal");
            text.Add("mods", string.Join(", ", missingOnLocal));
            builder.AppendLine(text.GetFormattedText());
        }

        return builder.ToString().Trim();
    }
}

internal readonly record struct OptionalNetworkFeatureIdentity(
    string ModId,
    string FeatureId,
    string CompatibilityVersion)
{
    internal static bool TryParse(string? token, out OptionalNetworkFeatureIdentity identity)
    {
        identity = default;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string[] parts = token.Split(':');
        if (parts.Length != 4
            || !string.Equals(
                parts[0],
                OptionalNetworkFeatureManager.CompatibilityTokenPrefix,
                StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            string modId = Uri.UnescapeDataString(parts[1]);
            string featureId = Uri.UnescapeDataString(parts[2]);
            string compatibilityVersion = Uri.UnescapeDataString(parts[3]);
            if (string.IsNullOrWhiteSpace(modId)
                || string.IsNullOrWhiteSpace(featureId)
                || string.IsNullOrWhiteSpace(compatibilityVersion))
            {
                return false;
            }

            identity = new OptionalNetworkFeatureIdentity(modId, featureId, compatibilityVersion);
            return true;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }
}

internal static class OptionalNetworkMismatchText
{
    private const string KeyPrefix = "EXTENSION.JMCMODLIB.OPTIONAL_NETWORK_MISMATCH";
    private static readonly Assembly ThisAssembly = typeof(OptionalNetworkMismatchText).Assembly;

    internal static string Title()
    {
        return Resolve("TITLE", "Optional multiplayer features do not match");
    }

    internal static string BuildBody(
        IReadOnlyCollection<OptionalNetworkFeatureDescriptor> localOnly,
        IReadOnlyCollection<OptionalNetworkFeatureDescriptor> hostOnly,
        string remainingMismatch)
    {
        List<string> sections =
        [
            Resolve(
                "INTRO",
                "The connection failed because the JML optional multiplayer features enabled on both sides are different.")
        ];

        if (localOnly.Count > 0)
        {
            string features = string.Join('\n', localOnly.Select(FormatFeature));
            sections.Add(Resolve(
                "LOCAL_ONLY",
                $"Enabled locally, but not enabled or installed by the host:\n{features}\n\nDisable these features and retry, or ask the host to install compatible MODs and enable the same features.",
                loc => loc.Add("features", features)));
        }

        if (hostOnly.Count > 0)
        {
            string features = string.Join('\n', hostOnly.Select(FormatFeature));
            sections.Add(Resolve(
                "HOST_ONLY",
                $"Enabled by the host, but not enabled or installed locally:\n{features}\n\nInstall compatible MODs and enable the same features, or ask the host to disable them.",
                loc => loc.Add("features", features)));
        }

        if (!string.IsNullOrWhiteSpace(remainingMismatch))
        {
            sections.Add(Resolve(
                "OTHER_MISMATCH",
                $"There are also ordinary MOD mismatches:\n{remainingMismatch}",
                loc => loc.Add("details", remainingMismatch)));
        }

        return string.Join("\n\n", sections);
    }

    private static string FormatFeature(OptionalNetworkFeatureDescriptor feature)
    {
        OptionalNetworkFeatureIdentity identity = feature.Identity;
        return Resolve(
            "FEATURE_LINE",
            $"• {feature.DisplayName} ({identity.ModId} / {identity.FeatureId}, protocol {identity.CompatibilityVersion})",
            loc =>
            {
                loc.Add("name", feature.DisplayName);
                loc.Add("modId", identity.ModId);
                loc.Add("featureId", identity.FeatureId);
                loc.Add("version", identity.CompatibilityVersion);
            });
    }

    private static string Resolve(string key, string fallback, Action<LocString>? configure = null)
    {
        return L10n.Resolve(
            $"{KeyPrefix}.{key}",
            fallback,
            L10n.DefaultTable,
            ThisAssembly,
            configure);
    }
}
