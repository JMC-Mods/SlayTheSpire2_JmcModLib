using Godot;
using HarmonyLib;
using JmcModLib.Compat;
using JmcModLib.Multiplayer.Internal;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Connection;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace JmcModLib.Multiplayer.Patches;

[HarmonyPatch(typeof(ReflectionHelper), nameof(ReflectionHelper.GetSubtypesInMods), [typeof(Type)])]
internal static class OptionalNetworkMessageFilterPatch
{
    [HarmonyPostfix]
    private static void Postfix(Type parentType, ref IEnumerable<Type> __result)
    {
        if (parentType == typeof(INetMessage))
        {
            __result = __result.Where(OptionalNetworkFeatureManager.ShouldIncludeMessage).ToArray();
        }
    }
}

[HarmonyPatch(typeof(OneTimeInitialization), nameof(OneTimeInitialization.ExecuteEssential))]
internal static class OptionalNetworkProtocolInitializationPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        OptionalNetworkFeatureManager.PrepareInitialProtocol();
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
        OptionalNetworkFeatureManager.MarkProtocolInitialized();
    }
}

[HarmonyPatch(typeof(ModManager), nameof(ModManager.GetGameplayRelevantModNameList))]
internal static class OptionalNetworkCompatibilityPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref List<string>? __result)
    {
        IReadOnlyList<string> tokens = OptionalNetworkFeatureManager.GetCompatibilityTokens();
        if (tokens.Count == 0)
        {
            return;
        }

        __result ??= [];
        foreach (string token in tokens)
        {
            if (!__result.Contains(token, StringComparer.Ordinal))
            {
                __result.Add(token);
            }
        }
    }
}

[HarmonyPatch(typeof(NErrorPopup), nameof(NErrorPopup.Create), [typeof(NetErrorInfo)])]
internal static class OptionalNetworkMismatchPopupPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NetErrorInfo info, ref NErrorPopup? __result)
    {
        try
        {
            if (!OptionalNetworkMismatchPresenter.TryCreatePopup(info, out NErrorPopup? popup))
            {
                return true;
            }

            __result = popup;
            return false;
        }
        catch (Exception ex)
        {
            ModLogger.Warn("生成可选联机功能不匹配提示失败，将使用游戏原始提示。", ex);
            return true;
        }
    }
}

[HarmonyPatch(
    typeof(JoinFlow),
    nameof(JoinFlow.Begin),
    [typeof(IClientConnectionInitializer), typeof(SceneTree)])]
internal static class OptionalNetworkJoinFlowPatch
{
    [HarmonyPrefix]
    private static void Prefix(JoinFlow __instance, ref INetGameService? __state)
    {
        // 0.107.1 在 Begin() 内部才创建 NetService，前缀阶段为空是正常状态。
        INetGameService? service = ResolveService(__instance, allowUninitialized: true);
        if (OptionalNetworkActivityTracker.TryTrack(service))
        {
            __state = service;
        }
    }

    [HarmonyPostfix]
    private static void Postfix(
        JoinFlow __instance,
        ref Task<JoinResult> __result,
        ref INetGameService? __state)
    {
        // async Begin() 返回 Task 前会同步执行到首个 await，0.107.1 此时已创建 NetService。
        INetGameService? service = ResolveService(__instance, allowUninitialized: false);
        if (__state == null && OptionalNetworkActivityTracker.TryTrack(service))
        {
            __state = service;
        }

        if (service != null)
        {
            __result = ObserveJoinAsync(__result, service, ReferenceEquals(__state, service));
        }
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception, INetGameService? __state)
    {
        if (__exception != null)
        {
            OptionalNetworkActivityTracker.ReleaseIfDisconnected(__state);
        }

        return __exception;
    }

    private static INetGameService? ResolveService(JoinFlow flow, bool allowUninitialized)
    {
        if (!MultiplayerCompat.TryReadJoinFlowNetService(flow, out INetGameService? service))
        {
            OptionalNetworkActivityTracker.MarkUnreliable("当前游戏版本缺少 JoinFlow.NetService，无法安全热重建网络协议。");
            return null;
        }

        if (service == null && !allowUninitialized)
        {
            OptionalNetworkActivityTracker.MarkUnreliable(
                "JoinFlow.Begin 返回后 NetService 仍未初始化，无法安全热重建网络协议。");
        }

        return service;
    }

    private static async Task<JoinResult> ObserveJoinAsync(
        Task<JoinResult> task,
        INetGameService service,
        bool ownsTracking)
    {
        try
        {
            JoinResult result = await task;
            if (!service.IsConnected && ownsTracking)
            {
                OptionalNetworkActivityTracker.Release(service);
            }

            return result;
        }
        catch
        {
            if (ownsTracking)
            {
                OptionalNetworkActivityTracker.ReleaseIfDisconnected(service);
            }

            throw;
        }
    }
}

[HarmonyPatch(typeof(NetHostGameService), nameof(NetHostGameService.StartENetHost), [typeof(ushort), typeof(int)])]
internal static class OptionalNetworkENetHostPatch
{
    [HarmonyPrefix]
    private static void Prefix(NetHostGameService __instance, ref bool __state)
    {
        __state = OptionalNetworkActivityTracker.TryTrack(__instance);
    }

    [HarmonyPostfix]
    private static void Postfix(NetHostGameService __instance, NetErrorInfo? __result, bool __state)
    {
        if (__state && __result.HasValue)
        {
            OptionalNetworkActivityTracker.ReleaseIfDisconnected(__instance);
        }
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(
        Exception? __exception,
        NetHostGameService __instance,
        bool __state)
    {
        if (__state && __exception != null)
        {
            OptionalNetworkActivityTracker.ReleaseIfDisconnected(__instance);
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(NetHostGameService), nameof(NetHostGameService.StartSteamHost), [typeof(int)])]
internal static class OptionalNetworkSteamHostPatch
{
    [HarmonyPrefix]
    private static void Prefix(NetHostGameService __instance, ref bool __state)
    {
        __state = OptionalNetworkActivityTracker.TryTrack(__instance);
    }

    [HarmonyPostfix]
    private static void Postfix(
        NetHostGameService __instance,
        ref Task<NetErrorInfo?> __result,
        bool __state)
    {
        if (__state)
        {
            __result = ObserveStartAsync(__result, __instance);
        }
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(
        Exception? __exception,
        NetHostGameService __instance,
        bool __state)
    {
        if (__state && __exception != null)
        {
            OptionalNetworkActivityTracker.ReleaseIfDisconnected(__instance);
        }

        return __exception;
    }

    private static async Task<NetErrorInfo?> ObserveStartAsync(
        Task<NetErrorInfo?> task,
        NetHostGameService service)
    {
        try
        {
            NetErrorInfo? result = await task;
            if (result.HasValue)
            {
                OptionalNetworkActivityTracker.ReleaseIfDisconnected(service);
            }

            return result;
        }
        catch
        {
            OptionalNetworkActivityTracker.ReleaseIfDisconnected(service);
            throw;
        }
    }
}
