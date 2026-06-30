using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using System.Reflection;
using System.Text;
using LayoutPreset = Godot.Control.LayoutPreset;
using MouseFilterEnum = Godot.Control.MouseFilterEnum;
using SizeFlags = Godot.Control.SizeFlags;

namespace JmcModLib.Prefabs;

/// <summary>
/// 通过游戏模态容器显示适合长文本、诊断报告和日志摘要的可滚动报告弹窗。
/// </summary>
public static class JmcReportPopup
{
    private const string MainMenuTable = "main_menu_ui";
    private const string ConfirmKey = "GENERIC_POPUP.confirm";

    /// <summary>
    /// 获取当前是否可以打开报告弹窗。
    /// </summary>
    public static bool IsAvailable =>
        NModalContainer.Instance is { OpenModal: null };

    /// <summary>
    /// 打开一个可滚动报告弹窗。
    /// </summary>
    /// <param name="options">弹窗标题、正文、按钮和显示选项。</param>
    /// <param name="assembly">可选所属程序集，用于日志上下文。</param>
    /// <returns>成功打开时返回可用于更新内容的句柄；模态容器不可用或已有弹窗时返回 <see langword="null"/>。</returns>
    public static JmcReportPopupHandle? Open(JmcReportPopupOptions options, Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Title);

        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(JmcReportPopup));
        NModalContainer? modalContainer = NModalContainer.Instance;
        if (modalContainer == null)
        {
            ModLogger.Warn("Cannot show report popup because NModalContainer is not ready.", resolvedAssembly);
            return null;
        }

        if (modalContainer.OpenModal != null)
        {
            ModLogger.Warn("Cannot show report popup because another modal is already open.", resolvedAssembly);
            return null;
        }

        var view = new JmcReportPopupView(options, resolvedAssembly);
        var handle = new JmcReportPopupHandle(view);
        view.AttachHandle(handle);
        view.PrepareForOpen();
        ModLogger.Info(
            $"准备打开报告弹窗。BodyLength={options.Body?.Length ?? 0} Buttons={options.Buttons?.Count ?? 0}",
            resolvedAssembly);

        try
        {
            modalContainer.Add(view, options.ShowBackstop);
            ModLogger.Info(
                $"报告弹窗已提交到模态容器。InTree={view.IsInsideTree()} Parent={view.GetParent()?.GetPath().ToString() ?? "<null>"} OpenModalMatches={ReferenceEquals(modalContainer.OpenModal, view)}",
                resolvedAssembly);
            view.RecoverModalLayout();
            if (!ReferenceEquals(modalContainer.OpenModal, view))
            {
                handle.NotifyClosed(view);
                view.QueueFree();
                ModLogger.Warn("Cannot show report popup because another modal opened first.", resolvedAssembly);
                return null;
            }

            return handle;
        }
        catch (Exception ex)
        {
            handle.NotifyClosed(view);
            if (ReferenceEquals(modalContainer.OpenModal, view))
            {
                modalContainer.Clear();
            }
            else
            {
                view.QueueFree();
            }

            ModLogger.Error("Failed to show report popup.", ex, resolvedAssembly);
            return null;
        }
    }

    internal static string DefaultCloseText()
    {
        try
        {
            return new LocString(MainMenuTable, ConfirmKey).GetFormattedText();
        }
        catch
        {
            return "OK";
        }
    }
}

/// <summary>
/// 报告弹窗正文的解析格式。
/// </summary>
public enum JmcReportPopupBodyFormat
{
    /// <summary>
    /// 按纯文本显示，正文中的游戏富文本标签会被转义。
    /// </summary>
    PlainText,

    /// <summary>
    /// 按游戏富文本标签解析正文。
    /// </summary>
    RichText,

    /// <summary>
    /// 按轻量 Markdown 解析，并转换为游戏富文本显示。
    /// </summary>
    Markdown
}

/// <summary>
/// 报告弹窗的显示选项。
/// </summary>
public sealed class JmcReportPopupOptions
{
    /// <summary>
    /// 弹窗标题。
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// 弹窗正文。默认按纯文本处理，方括号不会被当成富文本标签。
    /// </summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// 可选副标题，显示在标题下方。
    /// </summary>
    public string? Subtitle { get; init; }

    /// <summary>
    /// 可选状态文本，显示在底部按钮左侧。
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// 正文显示格式。默认按纯文本处理；显示 LLM 诊断报告时可使用 <see cref="JmcReportPopupBodyFormat.Markdown"/>。
    /// </summary>
    public JmcReportPopupBodyFormat BodyFormat { get; init; } = JmcReportPopupBodyFormat.PlainText;

    /// <summary>
    /// 正文是否按游戏富文本解析。保留用于兼容旧写法；新代码建议改用 <see cref="BodyFormat"/>。
    /// </summary>
    public bool BodyUsesRichText { get; init; }

    /// <summary>
    /// 底部按钮集合。为空时会自动添加一个关闭按钮。
    /// </summary>
    public IReadOnlyList<JmcReportPopupButton> Buttons { get; init; } = Array.Empty<JmcReportPopupButton>();

    /// <summary>
    /// 是否显示弹窗背后的游戏原生深色遮罩。
    /// </summary>
    public bool ShowBackstop { get; init; } = true;

    /// <summary>
    /// 是否允许玩家按 Escape 关闭弹窗。
    /// </summary>
    public bool CloseOnEscape { get; init; } = true;

    /// <summary>
    /// 弹窗面板的最小尺寸。
    /// </summary>
    public Vector2 MinimumSize { get; init; } = new(940f, 700f);
}

/// <summary>
/// 报告弹窗底部按钮的定义。
/// </summary>
public sealed class JmcReportPopupButton
{
    /// <summary>
    /// 创建一个报告弹窗按钮。
    /// </summary>
    /// <param name="key">按钮唯一键，用于后续通过句柄启用或禁用按钮。</param>
    /// <param name="text">按钮显示文本。</param>
    /// <param name="action">按钮点击回调。</param>
    /// <param name="closeOnClick">点击按钮后是否关闭弹窗。</param>
    /// <param name="enabled">按钮初始是否可用。</param>
    public JmcReportPopupButton(
        string key,
        string text,
        Action<JmcReportPopupHandle>? action = null,
        bool closeOnClick = false,
        bool enabled = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        Key = key.Trim();
        Text = text.Trim();
        Action = action;
        CloseOnClick = closeOnClick;
        Enabled = enabled;
    }

    /// <summary>
    /// 按钮唯一键，用于后续通过句柄启用或禁用按钮。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 按钮显示文本。
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 按钮点击回调。
    /// </summary>
    public Action<JmcReportPopupHandle>? Action { get; }

    /// <summary>
    /// 点击按钮后是否关闭弹窗。
    /// </summary>
    public bool CloseOnClick { get; }

    /// <summary>
    /// 按钮初始是否可用。
    /// </summary>
    public bool Enabled { get; }
}

/// <summary>
/// 已打开报告弹窗的操作句柄。
/// </summary>
public sealed class JmcReportPopupHandle
{
    private JmcReportPopupView? view;

    internal JmcReportPopupHandle(JmcReportPopupView view)
    {
        this.view = view;
    }

    /// <summary>
    /// 获取弹窗是否仍处于打开状态。
    /// </summary>
    public bool IsOpen => view?.IsOpen == true;

    /// <summary>
    /// 更新弹窗标题。
    /// </summary>
    /// <param name="title">新的标题文本。</param>
    public void SetTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        view?.SetTitle(title);
    }

    /// <summary>
    /// 更新弹窗副标题。
    /// </summary>
    /// <param name="subtitle">新的副标题文本；传入 <see langword="null"/> 或空白字符串会隐藏副标题。</param>
    public void SetSubtitle(string? subtitle)
    {
        view?.SetSubtitle(subtitle);
    }

    /// <summary>
    /// 更新弹窗底部状态文本。
    /// </summary>
    /// <param name="status">新的状态文本；传入 <see langword="null"/> 或空白字符串会隐藏状态文本。</param>
    public void SetStatus(string? status)
    {
        view?.SetStatus(status);
    }

    /// <summary>
    /// 更新弹窗正文，并沿用当前正文格式。
    /// </summary>
    /// <param name="body">新的正文文本。</param>
    public void SetBody(string body)
    {
        view?.SetBody(body, (JmcReportPopupBodyFormat?)null);
    }

    /// <summary>
    /// 更新弹窗正文，并同时指定是否按游戏富文本解析。
    /// </summary>
    /// <param name="body">新的正文文本。</param>
    /// <param name="bodyUsesRichText">正文是否按游戏富文本解析。</param>
    public void SetBody(string body, bool bodyUsesRichText)
    {
        view?.SetBody(body, bodyUsesRichText);
    }

    /// <summary>
    /// 更新弹窗正文，并同时指定正文解析格式。
    /// </summary>
    /// <param name="body">新的正文文本。</param>
    /// <param name="bodyFormat">正文解析格式。</param>
    public void SetBody(string body, JmcReportPopupBodyFormat bodyFormat)
    {
        view?.SetBody(body, bodyFormat);
    }

    /// <summary>
    /// 启用或禁用指定按钮。
    /// </summary>
    /// <param name="key">按钮唯一键。</param>
    /// <param name="enabled">是否启用按钮。</param>
    public void SetButtonEnabled(string key, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        view?.SetButtonEnabled(key, enabled);
    }

    /// <summary>
    /// 关闭弹窗。
    /// </summary>
    public void Close()
    {
        view?.Close();
    }

    internal void NotifyClosed(JmcReportPopupView closingView)
    {
        if (ReferenceEquals(view, closingView))
        {
            view = null;
        }
    }
}

internal sealed class JmcReportPopupView : Control, IScreenContext
{
    private const int PopupZIndex = 100;

    private readonly Assembly assembly;
    private readonly List<JmcReportPopupButton> buttons;
    private readonly bool closeOnEscape;
    private readonly Vector2 minimumSize;
    private readonly Dictionary<string, Button> buttonControls = new(StringComparer.Ordinal);

    private JmcReportPopupHandle? handle;
    private MegaRichTextLabel? titleLabel;
    private MegaRichTextLabel? subtitleLabel;
    private MegaRichTextLabel? bodyLabel;
    private MegaRichTextLabel? statusLabel;
    private PanelContainer? panel;
    private Button? defaultFocusedControl;
    private string title;
    private string? subtitle;
    private string body;
    private string? status;
    private JmcReportPopupBodyFormat bodyFormat;
    private bool layoutBuilt;

    public JmcReportPopupView(JmcReportPopupOptions options, Assembly assembly)
    {
        this.assembly = assembly;
        title = options.Title;
        subtitle = options.Subtitle;
        body = options.Body;
        status = options.Status;
        bodyFormat = ResolveInitialBodyFormat(options);
        closeOnEscape = options.CloseOnEscape;
        minimumSize = new Vector2(
            Math.Max(520f, options.MinimumSize.X),
            Math.Max(420f, options.MinimumSize.Y));
        buttons = NormalizeButtons(options.Buttons);
    }

    public bool IsOpen { get; private set; } = true;

    public Control? DefaultFocusedControl => defaultFocusedControl;

    public void AttachHandle(JmcReportPopupHandle popupHandle)
    {
        handle = popupHandle;
    }

    public override void _Ready()
    {
        try
        {
            ModLogger.Info("报告弹窗 Ready。", assembly);
            EnsureLayoutBuilt();
            RecoverModalLayout();
            RefreshAllText();
            Callable.From(RecoverModalLayout).CallDeferred();
            Callable.From(LogLayoutState).CallDeferred();

            Vector2 viewportSize = GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
            ModLogger.Info($"报告弹窗已创建。Viewport={viewportSize} Buttons={buttons.Count}", assembly);
        }
        catch (Exception ex)
        {
            ModLogger.Error("报告弹窗 Ready 失败。", ex, assembly);
        }
    }

    public override void _ExitTree()
    {
        IsOpen = false;
        handle?.NotifyClosed(this);
        base._ExitTree();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

        if (closeOnEscape && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport()?.SetInputAsHandled();
        }
    }

    public void SetTitle(string value)
    {
        title = value;
        RefreshTitle();
    }

    public void SetSubtitle(string? value)
    {
        subtitle = value;
        RefreshSubtitle();
    }

    public void SetStatus(string? value)
    {
        status = value;
        RefreshStatus();
    }

    public void SetBody(string value, bool? useRichText)
    {
        SetBody(
            value,
            useRichText.HasValue
                ? (useRichText.Value ? JmcReportPopupBodyFormat.RichText : JmcReportPopupBodyFormat.PlainText)
                : null);
    }

    public void SetBody(string value, JmcReportPopupBodyFormat? format)
    {
        body = value ?? string.Empty;
        if (format.HasValue)
        {
            bodyFormat = NormalizeBodyFormat(format.Value);
        }

        RefreshBody();
    }

    public void SetButtonEnabled(string key, bool enabled)
    {
        if (buttonControls.TryGetValue(key.Trim(), out Button? button))
        {
            button.Disabled = !enabled;
        }
    }

    public void Close()
    {
        NModalContainer? modalContainer = NModalContainer.Instance;
        if (modalContainer != null && ReferenceEquals(modalContainer.OpenModal, this))
        {
            modalContainer.Clear();
            return;
        }

        QueueFree();
    }

    internal void PrepareForOpen()
    {
        EnsureLayoutBuilt();
        RecoverModalLayout();
        RefreshAllText();
    }

    private void EnsureLayoutBuilt()
    {
        if (layoutBuilt)
        {
            return;
        }

        BuildLayout();
        layoutBuilt = true;
    }

    private void BuildLayout()
    {
        Name = "JmcReportPopup";
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = PopupZIndex;
        FillParent(this);

        var center = new CenterContainer();
        FillParent(center);
        AddChild(center);

        panel = new PanelContainer
        {
            Name = "JmcReportPopupPanel",
            CustomMinimumSize = minimumSize,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        titleLabel = CreateTextLabel();
        SetRichTextFontSize(titleLabel, 30);
        root.AddChild(titleLabel);

        subtitleLabel = CreateTextLabel();
        SetRichTextFontSize(subtitleLabel, 17);
        root.AddChild(subtitleLabel);

        root.AddChild(new HSeparator());

        Control scroll = BuildBodyScroll();
        scroll.CustomMinimumSize = new Vector2(0f, 360f);
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        root.AddChild(scroll);

        root.AddChild(new HSeparator());
        root.AddChild(BuildFooter());
    }

    internal void RecoverModalLayout()
    {
        Vector2 viewportSize = GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
        FillParent(this);
        if (viewportSize.X > 0f && viewportSize.Y > 0f)
        {
            Size = viewportSize;
        }

        Node? parent = GetParent();
        if (parent != null)
        {
            parent.MoveChildSafely(this, Math.Max(0, parent.GetChildCount() - 1));
        }
    }

    private void LogLayoutState()
    {
        Vector2 viewportSize = GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
        ModLogger.Info(
            $"报告弹窗布局。Root={Size} Panel={panel?.Size ?? Vector2.Zero} Viewport={viewportSize} Children={GetChildCount()}",
            assembly);
    }

    private static void FillParent(Control control)
    {
        control.SetAnchorsPreset(LayoutPreset.FullRect);
        control.OffsetLeft = 0f;
        control.OffsetTop = 0f;
        control.OffsetRight = 0f;
        control.OffsetBottom = 0f;
    }

    private static StyleBoxFlat CreatePanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.055f, 0.071f, 0.078f, 0.98f),
            BorderColor = new Color(0.57f, 0.78f, 0.74f, 0.82f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusBottomLeft = 6,
            ShadowColor = new Color(0f, 0f, 0f, 0.45f),
            ShadowSize = 18
        };
    }

    private Control BuildBodyScroll()
    {
        return BuildFallbackBodyScroll();
    }

    private Control BuildFallbackBodyScroll()
    {
        var scroll = new ScrollContainer
        {
            ClipContents = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.None
        };

        var margin = new MarginContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_right", 18);
        scroll.AddChild(margin);

        bodyLabel = CreateTextLabel();
        ConfigureBodyLabel(bodyLabel);
        margin.AddChild(bodyLabel);
        return scroll;
    }

    private Control BuildFooter()
    {
        var footer = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        footer.AddThemeConstantOverride("separation", 10);

        statusLabel = CreateTextLabel();
        SetRichTextFontSize(statusLabel, 15);
        statusLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(statusLabel);

        foreach (JmcReportPopupButton buttonOptions in buttons)
        {
            Button button = BuildButton(buttonOptions);
            footer.AddChild(button);
            buttonControls[buttonOptions.Key] = button;
            if (defaultFocusedControl == null && !button.Disabled)
            {
                defaultFocusedControl = button;
            }
        }

        defaultFocusedControl ??= buttonControls.Values.FirstOrDefault();
        return footer;
    }

    private Button BuildButton(JmcReportPopupButton buttonOptions)
    {
        var button = new Button
        {
            Text = buttonOptions.Text,
            Disabled = !buttonOptions.Enabled,
            CustomMinimumSize = new Vector2(140f, 42f)
        };

        button.Pressed += () =>
        {
            try
            {
                if (handle != null)
                {
                    buttonOptions.Action?.Invoke(handle);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Report popup button '{buttonOptions.Key}' failed.", ex, assembly);
            }

            if (buttonOptions.CloseOnClick)
            {
                Close();
            }
        };

        return button;
    }

    private static MegaRichTextLabel CreateTextLabel()
    {
        var label = new MegaRichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutoSizeEnabled = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };

        ApplyThemeFontOverrides(label);
        SetRichTextFontSize(label, 16);
        return label;
    }

    private static void ConfigureBodyLabel(MegaRichTextLabel label)
    {
        label.BbcodeEnabled = true;
        label.FitContent = true;
        label.ScrollActive = false;
        label.AutoSizeEnabled = false;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.MouseFilter = MouseFilterEnum.Ignore;
        ApplyThemeFontOverrides(label);
        SetRichTextFontSize(label, 16);
        label.AddThemeFontSizeOverride("mono_font_size", 15);
    }

    private static void ApplyThemeFontOverrides(MegaRichTextLabel label)
    {
        label.AddThemeFontOverride(
            ThemeConstants.RichTextLabel.NormalFont,
            label.GetThemeFont(ThemeConstants.RichTextLabel.NormalFont, "RichTextLabel"));
        label.AddThemeFontOverride(
            ThemeConstants.RichTextLabel.BoldFont,
            label.GetThemeFont(ThemeConstants.RichTextLabel.BoldFont, "RichTextLabel"));
        label.AddThemeFontOverride(
            ThemeConstants.RichTextLabel.ItalicsFont,
            label.GetThemeFont(ThemeConstants.RichTextLabel.ItalicsFont, "RichTextLabel"));
    }

    private static void SetRichTextFontSize(MegaRichTextLabel label, int fontSize)
    {
        label.Call("SetFontSize", fontSize);
    }

    private void RefreshAllText()
    {
        RefreshTitle();
        RefreshSubtitle();
        RefreshBody();
        RefreshStatus();
    }

    private void RefreshTitle()
    {
        titleLabel?.SetTextAutoSize($"[gold][b]{EscapeText(title)}[/b][/gold]");
    }

    private void RefreshSubtitle()
    {
        if (subtitleLabel == null)
        {
            return;
        }

        bool visible = !string.IsNullOrWhiteSpace(subtitle);
        subtitleLabel.Visible = visible;
        subtitleLabel.SetTextAutoSize(visible ? $"[color=#aab7bc]{EscapeText(subtitle!)}[/color]" : string.Empty);
    }

    private void RefreshBody()
    {
        bodyLabel?.SetTextAutoSize(RenderBody());
    }

    private string RenderBody()
    {
        return NormalizeBodyFormat(bodyFormat) switch
        {
            JmcReportPopupBodyFormat.RichText => body,
            JmcReportPopupBodyFormat.Markdown => JmcReportMarkdownRenderer.Render(body),
            _ => EscapeText(body)
        };
    }

    private void RefreshStatus()
    {
        if (statusLabel == null)
        {
            return;
        }

        bool visible = !string.IsNullOrWhiteSpace(status);
        statusLabel.Visible = visible;
        statusLabel.SetTextAutoSize(visible ? $"[color=#aab7bc]{EscapeText(status!)}[/color]" : string.Empty);
    }

    private static string EscapeText(string text)
    {
        return text.EscapeBbcodeTags();
    }

    private static JmcReportPopupBodyFormat ResolveInitialBodyFormat(JmcReportPopupOptions options)
    {
        if (options.BodyUsesRichText && options.BodyFormat == JmcReportPopupBodyFormat.PlainText)
        {
            return JmcReportPopupBodyFormat.RichText;
        }

        return NormalizeBodyFormat(options.BodyFormat);
    }

    private static JmcReportPopupBodyFormat NormalizeBodyFormat(JmcReportPopupBodyFormat format)
    {
        return Enum.IsDefined(format)
            ? format
            : JmcReportPopupBodyFormat.PlainText;
    }

    private static List<JmcReportPopupButton> NormalizeButtons(IReadOnlyList<JmcReportPopupButton>? configuredButtons)
    {
        if (configuredButtons is { Count: > 0 })
        {
            return [.. configuredButtons];
        }

        return
        [
            new JmcReportPopupButton(
                "close",
                JmcReportPopup.DefaultCloseText(),
                closeOnClick: true)
        ];
    }
}

internal static class JmcReportMarkdownRenderer
{
    private const string HeadingColor = "#f4c84a";
    private const string SecondaryHeadingColor = "#e2d38f";
    private const string MutedColor = "#aab7bc";
    private const string CodeColor = "#d8e2e7";
    private const string LinkColor = "#78dce8";
    private const string RuleColor = "#42545c";
    private const string LogWarnColor = "#ffd166";
    private const string LogErrorColor = "#ff6b6b";
    private const string QuotePrefix = "    |  ";

    public static string Render(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        string normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        var builder = new StringBuilder(normalized.Length + 64);
        bool inCodeFence = false;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (IsFence(trimmed))
            {
                inCodeFence = !inCodeFence;
                AppendLineIfNeeded(builder);
                continue;
            }

            if (inCodeFence)
            {
                AppendCodeFenceLine(builder, line);
                continue;
            }

            AppendMarkdownLine(builder, line, trimmed);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendMarkdownLine(StringBuilder builder, string line, string trimmed)
    {
        if (trimmed.Length == 0)
        {
            AppendLineIfNeeded(builder);
            return;
        }

        if (TryAppendHeading(builder, trimmed)
            || TryAppendRule(builder, trimmed)
            || TryAppendLogLine(builder, line, indentAsCode: false)
            || TryAppendQuote(builder, trimmed)
            || TryAppendListItem(builder, line))
        {
            return;
        }

        builder.AppendLine(RenderInline(line.TrimEnd()));
    }

    private static bool TryAppendHeading(StringBuilder builder, string trimmed)
    {
        int level = 0;
        while (level < trimmed.Length && trimmed[level] == '#')
        {
            level++;
        }

        if (level is < 1 or > 3 || level >= trimmed.Length || trimmed[level] != ' ')
        {
            return false;
        }

        string text = RenderInline(trimmed[(level + 1)..].Trim());
        string color = level == 1 ? HeadingColor : SecondaryHeadingColor;
        builder.Append("[color=");
        builder.Append(color);
        builder.Append("][b]");
        builder.Append(text);
        builder.AppendLine("[/b][/color]");
        return true;
    }

    private static bool TryAppendRule(StringBuilder builder, string trimmed)
    {
        if (trimmed.Length < 3)
        {
            return false;
        }

        char marker = trimmed[0];
        if (marker is not ('-' or '*' or '_'))
        {
            return false;
        }

        for (int i = 0; i < trimmed.Length; i++)
        {
            if (trimmed[i] != marker)
            {
                return false;
            }
        }

        builder.Append("[color=");
        builder.Append(RuleColor);
        builder.AppendLine("]----------------------------------------[/color]");
        return true;
    }

    private static bool TryAppendQuote(StringBuilder builder, string trimmed)
    {
        if (!trimmed.StartsWith("> ", StringComparison.Ordinal) && trimmed != ">")
        {
            return false;
        }

        string quote = trimmed.Length > 1 ? trimmed[2..].Trim() : string.Empty;
        builder.Append("[color=");
        builder.Append(MutedColor);
        builder.Append("]");
        builder.Append(QuotePrefix);
        builder.Append(RenderInline(quote));
        builder.AppendLine("[/color]");
        return true;
    }

    private static void AppendCodeFenceLine(StringBuilder builder, string line)
    {
        if (TryAppendLogLine(builder, line, indentAsCode: true))
        {
            return;
        }

        builder.Append("[color=");
        builder.Append(CodeColor);
        builder.Append("]    ");
        builder.Append(Escape(line));
        builder.AppendLine("[/color]");
    }

    private static bool TryAppendLogLine(StringBuilder builder, string line, bool indentAsCode)
    {
        string text = line.TrimEnd();
        if (!TryGetLogLineColor(text, out string color))
        {
            return false;
        }

        builder.Append("[color=");
        builder.Append(color);
        builder.Append(']');
        if (indentAsCode)
        {
            builder.Append("    ");
        }

        builder.Append(Escape(text));
        builder.AppendLine("[/color]");
        return true;
    }

    private static bool TryGetLogLineColor(string line, out string color)
    {
        string trimmed = line.TrimStart();
        if (StartsLogMarker(trimmed, "[ERROR]")
            || StartsLogMarker(trimmed, "[ERR]")
            || StartsLogMarker(trimmed, "[FATAL]")
            || StartsLogMarker(trimmed, "ERROR:")
            || StartsLogMarker(trimmed, "FATAL:"))
        {
            color = LogErrorColor;
            return true;
        }

        if (StartsLogMarker(trimmed, "[WARN]")
            || StartsLogMarker(trimmed, "[WARNING]")
            || StartsLogMarker(trimmed, "WARN:")
            || StartsLogMarker(trimmed, "WARNING:"))
        {
            color = LogWarnColor;
            return true;
        }

        color = string.Empty;
        return false;
    }

    private static bool StartsLogMarker(string line, string marker)
    {
        return line.StartsWith(marker, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryAppendListItem(StringBuilder builder, string line)
    {
        string trimmedStart = line.TrimStart();
        int indent = line.Length - trimmedStart.Length;
        if (trimmedStart.Length < 3)
        {
            return false;
        }

        if (trimmedStart[1] == ' ' && trimmedStart[0] is '-' or '*' or '+')
        {
            AppendListIndent(builder, indent);
            builder.Append("- ");
            builder.AppendLine(RenderInline(trimmedStart[2..].Trim()));
            return true;
        }

        int dotIndex = trimmedStart.IndexOf(". ", StringComparison.Ordinal);
        if (dotIndex <= 0)
        {
            return false;
        }

        for (int i = 0; i < dotIndex; i++)
        {
            if (!char.IsDigit(trimmedStart[i]))
            {
                return false;
            }
        }

        AppendListIndent(builder, indent);
        builder.Append(Escape(trimmedStart[..(dotIndex + 2)]));
        builder.AppendLine(RenderInline(trimmedStart[(dotIndex + 2)..].Trim()));
        return true;
    }

    private static string RenderInline(string text)
    {
        var builder = new StringBuilder(text.Length + 24);
        for (int i = 0; i < text.Length;)
        {
            if (TryAppendInlineCode(builder, text, ref i)
                || TryAppendLink(builder, text, ref i)
                || TryAppendBold(builder, text, ref i)
                || TryAppendItalic(builder, text, ref i))
            {
                continue;
            }

            builder.Append(Escape(text[i].ToString()));
            i++;
        }

        return builder.ToString();
    }

    private static bool TryAppendInlineCode(StringBuilder builder, string text, ref int index)
    {
        if (text[index] != '`')
        {
            return false;
        }

        int end = text.IndexOf('`', index + 1);
        if (end <= index + 1)
        {
            return false;
        }

        builder.Append("[color=");
        builder.Append(CodeColor);
        builder.Append(']');
        builder.Append(Escape(text[(index + 1)..end]));
        builder.Append("[/color]");
        index = end + 1;
        return true;
    }

    private static bool TryAppendLink(StringBuilder builder, string text, ref int index)
    {
        if (text[index] != '[')
        {
            return false;
        }

        int labelEnd = text.IndexOf("](", index, StringComparison.Ordinal);
        if (labelEnd <= index + 1)
        {
            return false;
        }

        int urlEnd = text.IndexOf(')', labelEnd + 2);
        if (urlEnd <= labelEnd + 2)
        {
            return false;
        }

        string label = text[(index + 1)..labelEnd];
        string url = text[(labelEnd + 2)..urlEnd];
        builder.Append("[color=");
        builder.Append(LinkColor);
        builder.Append(']');
        builder.Append(Escape(label));
        builder.Append("[/color]");
        builder.Append(" [color=");
        builder.Append(MutedColor);
        builder.Append(']');
        builder.Append(Escape($"({url})"));
        builder.Append("[/color]");
        index = urlEnd + 1;
        return true;
    }

    private static bool TryAppendBold(StringBuilder builder, string text, ref int index)
    {
        if (index + 1 >= text.Length || text[index] != '*' || text[index + 1] != '*')
        {
            return false;
        }

        int end = text.IndexOf("**", index + 2, StringComparison.Ordinal);
        if (end <= index + 2)
        {
            return false;
        }

        builder.Append("[b]");
        builder.Append(RenderInline(text[(index + 2)..end]));
        builder.Append("[/b]");
        index = end + 2;
        return true;
    }

    private static bool TryAppendItalic(StringBuilder builder, string text, ref int index)
    {
        if (text[index] != '*' || index + 1 >= text.Length || text[index + 1] == '*')
        {
            return false;
        }

        int end = text.IndexOf('*', index + 1);
        if (end <= index + 1)
        {
            return false;
        }

        builder.Append("[i]");
        builder.Append(RenderInline(text[(index + 1)..end]));
        builder.Append("[/i]");
        index = end + 1;
        return true;
    }

    private static bool IsFence(string trimmed)
    {
        return trimmed.StartsWith("```", StringComparison.Ordinal)
            || trimmed.StartsWith("~~~", StringComparison.Ordinal);
    }

    private static void AppendListIndent(StringBuilder builder, int indent)
    {
        int level = Math.Min(3, Math.Max(0, indent / 2));
        for (int i = 0; i < level; i++)
        {
            builder.Append("  ");
        }
    }

    private static void AppendLineIfNeeded(StringBuilder builder)
    {
        if (builder.Length > 0 && builder[^1] != '\n')
        {
            builder.AppendLine();
        }

        builder.AppendLine();
    }

    private static string Escape(string text)
    {
        return text.EscapeBbcodeTags();
    }
}
