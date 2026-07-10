using Godot;
using HarmonyLib;
using JmcModLib.Multiplayer.Internal;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Connection;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using System.Reflection;

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

[HarmonyPatch(
    typeof(JoinFlow),
    nameof(JoinFlow.Begin),
    [typeof(IClientConnectionInitializer), typeof(SceneTree)])]
internal static class OptionalNetworkJoinFlowPatch
{
    private static readonly MethodInfo? NetServiceGetter =
        AccessTools.PropertyGetter(typeof(JoinFlow), "NetService");

    [HarmonyPrefix]
    private static void Prefix(JoinFlow __instance, ref INetGameService? __state)
    {
        INetGameService? service = ResolveService(__instance);
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
        INetGameService? service = ResolveService(__instance);
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

    private static INetGameService? ResolveService(JoinFlow flow)
    {
        if (NetServiceGetter == null)
        {
            OptionalNetworkActivityTracker.MarkUnreliable("当前游戏版本缺少 JoinFlow.NetService，无法安全热重建网络协议。");
            return null;
        }

        try
        {
            return NetServiceGetter.Invoke(flow, null) as INetGameService;
        }
        catch (Exception ex)
        {
            OptionalNetworkActivityTracker.MarkUnreliable(
                "读取 JoinFlow 网络服务失败，将保留旧协议直到重启。",
                ex);
            return null;
        }
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
