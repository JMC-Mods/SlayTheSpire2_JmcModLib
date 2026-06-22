// 文件用途：为 Dispatch Bootstrap 提供不会阻断加载流程的日志封装。
using MegaCrit.Sts2.Core.Logging;

namespace JmcModLib.Dispatch.Bootstrap;

internal static class BootstrapLog
{
    private const string Prefix = "[JmcModLib.Dispatch] ";

    public static void Info(string message)
    {
        Write(LogLevel.Info, message);
    }

    public static void Warn(string message)
    {
        Write(LogLevel.Warn, message);
    }

    public static void Error(string message)
    {
        Write(LogLevel.Error, message);
    }

    private static void Write(LogLevel level, string message)
    {
        try
        {
            Log.LogMessage(level, LogType.Generic, Prefix + message);
        }
        catch
        {
            try
            {
                Console.WriteLine(Prefix + message);
            }
            catch
            {
                // 日志不能影响版本分派入口。
            }
        }
    }
}
