using MegaCrit.Sts2.Core.Modding;
using System.Reflection;

namespace JmcModLib.Config.UI;

internal static class ModConfigUiBridge
{
    internal static bool HasConfig(Mod? mod)
    {
        return GetConfigAssembly(mod) != null;
    }

    internal static Assembly? GetConfigAssembly(Mod? mod)
    {
        Assembly? directAssembly = ModAssemblyCompat.GetAssemblies(mod)
            .FirstOrDefault(static assembly => ConfigManager.GetEntries(assembly).Count > 0);
        if (directAssembly != null)
        {
            return directAssembly;
        }

        string? modId = mod?.manifest?.id;
        if (ModRegistry.TryGetContextByModId(modId, out ModContext? context)
            && context != null
            && context.IsCompleted
            && ConfigManager.GetEntries(context.Assembly).Count > 0)
        {
            return context.Assembly;
        }

        return null;
    }
}
