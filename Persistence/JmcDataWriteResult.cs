namespace JmcModLib.Persistence;

/// <summary>
/// 表示一次持久化槽位写入请求的结果。
/// </summary>
public readonly struct JmcDataWriteResult
{
    private JmcDataWriteResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    /// <summary>
    /// 写入请求是否已被 JML 接受。
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// 写入结果说明；成功时通常为空字符串。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 创建一个成功结果。
    /// </summary>
    /// <returns>成功结果。</returns>
    public static JmcDataWriteResult Succeeded()
    {
        return new JmcDataWriteResult(true, string.Empty);
    }

    /// <summary>
    /// 创建一个失败结果。
    /// </summary>
    /// <param name="message">失败原因。</param>
    /// <returns>失败结果。</returns>
    public static JmcDataWriteResult Failed(string message)
    {
        return new JmcDataWriteResult(false, message);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Success ? "Success" : $"Failed: {Message}";
    }
}
