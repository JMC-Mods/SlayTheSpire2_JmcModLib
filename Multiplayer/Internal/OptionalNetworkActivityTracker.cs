using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace JmcModLib.Multiplayer.Internal;

internal static class OptionalNetworkActivityTracker
{
    private static readonly object Gate = new();
    private static readonly Dictionary<INetGameService, Action<NetErrorInfo>> ActiveServices =
        new(ReferenceEqualityComparer.Instance);
    private static int trackingReliable = 1;

    internal static bool IsTrackingReliable => Volatile.Read(ref trackingReliable) == 1;

    internal static bool IsIdle
    {
        get
        {
            lock (Gate)
            {
                return ActiveServices.Count == 0;
            }
        }
    }

    internal static bool TryTrack(INetGameService? service)
    {
        if (service == null)
        {
            return false;
        }

        try
        {
            if (service.Type == NetGameType.Singleplayer)
            {
                return false;
            }

            lock (Gate)
            {
                if (ActiveServices.ContainsKey(service))
                {
                    return false;
                }

                Action<NetErrorInfo> disconnected = _ => Release(service);
                ActiveServices.Add(service, disconnected);
                try
                {
                    service.Disconnected += disconnected;
                }
                catch
                {
                    ActiveServices.Remove(service);
                    throw;
                }

                ModLogger.Debug($"开始追踪网络协议快照：{service.Type}。");
                return true;
            }
        }
        catch (Exception ex)
        {
            MarkUnreliable("订阅网络服务生命周期失败。", ex);
            return false;
        }
    }

    internal static void Release(INetGameService? service)
    {
        if (service == null)
        {
            return;
        }

        bool becameIdle;
        lock (Gate)
        {
            if (!ActiveServices.Remove(service, out Action<NetErrorInfo>? disconnected))
            {
                return;
            }

            try
            {
                service.Disconnected -= disconnected;
            }
            catch (Exception ex)
            {
                ModLogger.Warn("取消订阅网络服务生命周期失败。", ex);
            }

            becameIdle = ActiveServices.Count == 0;
            ModLogger.Debug($"结束追踪网络协议快照：{service.GetType().Name}。");
        }

        if (becameIdle)
        {
            Callable.From(NotifyIdleDeferred).CallDeferred();
        }
    }

    internal static void ReleaseIfDisconnected(INetGameService? service)
    {
        if (service == null)
        {
            return;
        }

        try
        {
            if (!service.IsConnected)
            {
                Release(service);
            }
        }
        catch (Exception ex)
        {
            MarkUnreliable("检查网络服务断开状态失败，将保留旧协议直到重启。", ex);
        }
    }

    internal static void MarkUnreliable(string message, Exception? exception = null)
    {
        Interlocked.Exchange(ref trackingReliable, 0);
        if (exception == null)
        {
            ModLogger.Error(message);
        }
        else
        {
            ModLogger.Error(message, exception);
        }
    }

    private static void NotifyIdleDeferred()
    {
        if (IsIdle)
        {
            OptionalNetworkFeatureManager.OnNetworkBecameIdle();
        }
    }
}
