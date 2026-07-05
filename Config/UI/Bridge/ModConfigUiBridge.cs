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
        return ModAssemblyCompat.GetAssemblies(mod)
            .FirstOrDefault(static assembly => ConfigManager.GetEntries(assembly).Count > 0);
    }
}
