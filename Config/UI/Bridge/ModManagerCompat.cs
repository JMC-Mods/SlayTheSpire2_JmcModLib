using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Modding;
using System.Collections;
using System.Reflection;

namespace JmcModLib.Config.UI;

internal static class ModManagerCompat
{
    private static readonly Lazy<Func<object?>> SettingsModListAccessor = new(CreateSettingsModListAccessor);
    private static bool loggedMissingModList;

    internal static IEnumerable<Mod> GetSettingsMods()
    {
        object? value = null;
        bool failed = false;
        try
        {
            value = SettingsModListAccessor.Value();
        }
        catch (Exception ex)
        {
            ModLogger.Warn("读取游戏 MOD 列表失败，JML 设置页将不显示子 MOD 配置。", ex);
            failed = true;
        }

        if (failed)
        {
            yield break;
        }

        if (value is not IEnumerable enumerable)
        {
            LogMissingModListOnce();
            yield break;
        }

        foreach (object? item in enumerable)
        {
            if (item is Mod mod)
            {
                yield return mod;
            }
        }
    }

    private static Func<object?> CreateSettingsModListAccessor()
    {
        foreach (string memberName in new[] { "Mods", "AllMods", "LoadedMods" })
        {
            MemberAccessor? accessor = TryGetStaticMember(typeof(ModManager), memberName);
            if (accessor != null)
            {
                return () => accessor.GetValue(null);
            }
        }

        MethodInfo? getLoadedMods = typeof(ModManager).GetMethod(
            "GetLoadedMods",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            Type.EmptyTypes);
        if (getLoadedMods != null)
        {
            return () => getLoadedMods.Invoke(null, null);
        }

        return () => null;
    }

    private static MemberAccessor? TryGetStaticMember(Type type, string memberName)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            try
            {
                MemberAccessor accessor = MemberAccessor.Get(current, memberName);
                if (accessor is { IsStatic: true })
                {
                    return accessor;
                }
            }
            catch (MissingMemberException)
            {
            }
        }

        return null;
    }

    private static void LogMissingModListOnce()
    {
        if (loggedMissingModList)
        {
            return;
        }

        loggedMissingModList = true;
        ModLogger.Warn("当前游戏版本未暴露可识别的 MOD 列表接口，JML 设置页将不显示子 MOD 配置。");
    }
}
