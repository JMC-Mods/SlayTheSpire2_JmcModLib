namespace JmcModLib.Config.UI;

/// <summary>
/// 指定配置项在设置 UI 中何时显示。
/// </summary>
/// <remarks>
/// 该 Attribute 只影响 UI 中的显示状态，不影响配置项注册、读取、写入或持久化。
/// 未声明该 Attribute 的配置项会保持默认行为：始终显示。
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class UIVisibleWhenAttribute : Attribute
{
    /// <summary>
    /// 创建布尔依赖可见性声明；依赖配置项转换为 <see langword="true"/> 时显示。
    /// </summary>
    /// <param name="dependsOn">依赖的配置成员名、存储 key 或完整运行时 key。</param>
    public UIVisibleWhenAttribute(string dependsOn)
        : this(dependsOn, true)
    {
    }

    /// <summary>
    /// 创建布尔值相等可见性声明。
    /// </summary>
    /// <param name="dependsOn">依赖的配置成员名、存储 key 或完整运行时 key。</param>
    /// <param name="expectedValue">依赖配置项等于该布尔值时显示。</param>
    public UIVisibleWhenAttribute(string dependsOn, bool expectedValue)
        : this(dependsOn, (object)expectedValue)
    {
    }

    /// <summary>
    /// 创建文本值相等可见性声明。
    /// </summary>
    /// <param name="dependsOn">依赖的配置成员名、存储 key 或完整运行时 key。</param>
    /// <param name="expectedValue">依赖配置项等于该文本值时显示；若依赖项为 enum，会按成员名解析。</param>
    public UIVisibleWhenAttribute(string dependsOn, string expectedValue)
        : this(dependsOn, (object)expectedValue)
    {
    }

    /// <summary>
    /// 创建整数值相等可见性声明。
    /// </summary>
    /// <param name="dependsOn">依赖的配置成员名、存储 key 或完整运行时 key。</param>
    /// <param name="expectedValue">依赖配置项等于该整数值时显示。</param>
    public UIVisibleWhenAttribute(string dependsOn, int expectedValue)
        : this(dependsOn, (object)expectedValue)
    {
    }

    /// <summary>
    /// 创建浮点值相等可见性声明。
    /// </summary>
    /// <param name="dependsOn">依赖的配置成员名、存储 key 或完整运行时 key。</param>
    /// <param name="expectedValue">依赖配置项等于该浮点值时显示。</param>
    public UIVisibleWhenAttribute(string dependsOn, double expectedValue)
        : this(dependsOn, (object)expectedValue)
    {
    }

    private UIVisibleWhenAttribute(string dependsOn, object expectedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependsOn);
        DependsOn = dependsOn.Trim();
        ExpectedValue = expectedValue;
    }

    /// <summary>
    /// 依赖的配置成员名、存储 key 或完整运行时 key。
    /// </summary>
    public string DependsOn { get; }

    /// <summary>
    /// 依赖配置项应匹配的目标值。
    /// </summary>
    public object ExpectedValue { get; }

    /// <summary>
    /// 是否反转判断结果；为 <see langword="true"/> 时，匹配目标值会隐藏，不匹配时显示。
    /// </summary>
    public bool Invert { get; set; }

    /// <summary>
    /// 文本值比较时是否忽略大小写。
    /// </summary>
    public bool IgnoreCase { get; set; } = true;
}
