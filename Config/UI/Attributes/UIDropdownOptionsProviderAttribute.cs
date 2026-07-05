namespace JmcModLib.Config.UI;

/// <summary>
/// 指定下拉配置项的运行时候选项提供器。
/// </summary>
/// <remarks>
/// 该 Attribute 应与 <see cref="UIDropdownAttribute"/> 配合使用。提供器可以是同一配置类型中的静态方法或静态属性；
/// 方法可以不带参数，也可以接收一个 <see cref="IConfigUiContext"/> 参数用于读取当前 MOD 的其他配置项。
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class UIDropdownOptionsProviderAttribute : Attribute
{
    /// <summary>
    /// 创建下拉候选项提供器声明。
    /// </summary>
    /// <param name="providerName">同一配置类型中的静态方法或静态属性名称。</param>
    public UIDropdownOptionsProviderAttribute(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ProviderName = providerName.Trim();
    }

    /// <summary>
    /// 创建下拉候选项提供器声明，并声明触发刷新所依赖的配置成员。
    /// </summary>
    /// <param name="providerName">同一配置类型中的静态方法或静态属性名称。</param>
    /// <param name="dependsOn">候选项依赖的配置成员名、存储 key 或完整运行时 key。</param>
    public UIDropdownOptionsProviderAttribute(string providerName, params string[] dependsOn)
        : this(providerName)
    {
        DependsOn = dependsOn ?? [];
    }

    /// <summary>
    /// 同一配置类型中的静态方法或静态属性名称。
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// 候选项依赖的配置成员名、存储 key 或完整运行时 key；这些配置项变化后，JML 会刷新当前下拉列表。
    /// </summary>
    public string[] DependsOn { get; set; } = [];

    /// <summary>
    /// 当前值不在新候选项列表中时的处理策略。
    /// </summary>
    public UIDropdownInvalidValuePolicy InvalidValuePolicy { get; set; } = UIDropdownInvalidValuePolicy.KeepCurrent;
}

/// <summary>
/// 动态下拉候选项变化后，当前值不再存在时的处理策略。
/// </summary>
public enum UIDropdownInvalidValuePolicy
{
    /// <summary>
    /// 保留当前值，并在 UI 中继续显示该值。
    /// </summary>
    KeepCurrent,

    /// <summary>
    /// 自动选择新的候选项列表中的第一个值。
    /// </summary>
    SelectFirstAvailable,

    /// <summary>
    /// 将配置项重置为默认值。
    /// </summary>
    ResetToDefault
}

/// <summary>
/// 配置 UI 运行时上下文，用于动态候选项或动态 UI 状态判断读取当前 MOD 的其他配置项。
/// </summary>
public interface IConfigUiContext
{
    /// <summary>
    /// 读取指定配置项的当前值。
    /// </summary>
    /// <typeparam name="T">期望读取的值类型。</typeparam>
    /// <param name="key">配置成员名、存储 key 或完整运行时 key。</param>
    /// <returns>转换为 <typeparamref name="T"/> 后的当前值。</returns>
    T Get<T>(string key);

    /// <summary>
    /// 尝试读取指定配置项的当前值。
    /// </summary>
    /// <typeparam name="T">期望读取的值类型。</typeparam>
    /// <param name="key">配置成员名、存储 key 或完整运行时 key。</param>
    /// <param name="value">读取成功时返回转换后的当前值。</param>
    /// <returns>找到并成功转换配置项时为 <see langword="true"/>。</returns>
    bool TryGet<T>(string key, out T value);

    /// <summary>
    /// 读取指定配置项的当前原始值。
    /// </summary>
    /// <param name="key">配置成员名、存储 key 或完整运行时 key。</param>
    /// <returns>当前原始值。</returns>
    object? Get(string key);

    /// <summary>
    /// 尝试读取指定配置项的当前原始值。
    /// </summary>
    /// <param name="key">配置成员名、存储 key 或完整运行时 key。</param>
    /// <param name="value">读取成功时返回当前原始值。</param>
    /// <returns>找到配置项时为 <see langword="true"/>。</returns>
    bool TryGet(string key, out object? value);
}
