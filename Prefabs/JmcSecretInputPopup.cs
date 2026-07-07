using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using System.Reflection;
using LayoutPreset = Godot.Control.LayoutPreset;
using MouseFilterEnum = Godot.Control.MouseFilterEnum;
using SizeFlags = Godot.Control.SizeFlags;

namespace JmcModLib.Prefabs;

/// <summary>
/// Secret 输入弹窗的显示选项。
/// </summary>
public sealed class JmcSecretInputPopupOptions
{
    /// <summary>
    /// 弹窗标题。
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// 弹窗说明文本。
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 输入框占位文本。
    /// </summary>
    public string? Placeholder { get; init; }

    /// <summary>
    /// 确认按钮文本。
    /// </summary>
    public string? ConfirmText { get; init; }

    /// <summary>
    /// 取消按钮文本。
    /// </summary>
    public string? CancelText { get; init; }

    /// <summary>
    /// 输入为空时显示的提示文本。
    /// </summary>
    public string? EmptyText { get; init; }

    /// <summary>
    /// 当前 Secret 后端保护等级，用于展示风险提示。
    /// </summary>
    public JmcModLib.Security.JmcSecretProtectionLevel ProtectionLevel { get; init; }

    /// <summary>
    /// 是否显示弹窗背后的原生深色模态遮罩。
    /// </summary>
    public bool ShowBackstop { get; init; } = true;

    /// <summary>
    /// 弹窗面板的最小尺寸。
    /// </summary>
    public Vector2 MinimumSize { get; init; } = new(720f, 360f);
}

/// <summary>
/// 通过游戏模态容器显示 Secret 输入框。
/// </summary>
public static class JmcSecretInputPopup
{
    /// <summary>
    /// 获取当前是否可以打开 Secret 输入弹窗。
    /// </summary>
    public static bool IsAvailable =>
        NModalContainer.Instance is { OpenModal: null };

    /// <summary>
    /// 打开 Secret 输入弹窗。
    /// </summary>
    /// <param name="options">弹窗显示选项。</param>
    /// <param name="assembly">可选所属程序集，用于日志上下文。</param>
    /// <returns>确认时返回输入内容；取消、关闭或弹窗不可用时返回 <see langword="null"/>。</returns>
    public static Task<string?> PromptAsync(JmcSecretInputPopupOptions options, Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Title);

        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(JmcSecretInputPopup));
        NModalContainer? modalContainer = NModalContainer.Instance;
        if (modalContainer == null)
        {
            ModLogger.Warn("无法显示 Secret 输入弹窗：NModalContainer 尚未就绪。", resolvedAssembly);
            return Task.FromResult<string?>(null);
        }

        if (modalContainer.OpenModal != null)
        {
            ModLogger.Warn("无法显示 Secret 输入弹窗：当前已有其它模态窗口。", resolvedAssembly);
            return Task.FromResult<string?>(null);
        }

        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var view = new JmcSecretInputPopupView(options, completion, resolvedAssembly);
        view.PrepareForOpen();
        try
        {
            modalContainer.Add(view, options.ShowBackstop);
            view.RecoverModalLayout();
            if (!ReferenceEquals(modalContainer.OpenModal, view))
            {
                completion.TrySetResult(null);
                view.QueueFree();
            }
        }
        catch (Exception ex)
        {
            completion.TrySetResult(null);
            if (ReferenceEquals(modalContainer.OpenModal, view))
            {
                modalContainer.Clear();
            }
            else
            {
                view.QueueFree();
            }

            ModLogger.Error("显示 Secret 输入弹窗失败。", ex, resolvedAssembly);
        }

        return completion.Task;
    }
}

internal sealed class JmcSecretInputPopupView : Control, IScreenContext
{
    private const int PopupZIndex = 100;

    private readonly JmcSecretInputPopupOptions options;
    private readonly TaskCompletionSource<string?> completion;
    private readonly Assembly assembly;

    private LineEdit? input;
    private Label? statusLabel;
    private Button? confirmButton;
    private PanelContainer? panel;
    private bool completed;
    private bool layoutBuilt;

    public JmcSecretInputPopupView(
        JmcSecretInputPopupOptions options,
        TaskCompletionSource<string?> completion,
        Assembly assembly)
    {
        this.options = options;
        this.completion = completion;
        this.assembly = assembly;
    }

    public Control? DefaultFocusedControl => input;

    public override void _Ready()
    {
        try
        {
            EnsureLayoutBuilt();
            RecoverModalLayout();
            Callable.From(RecoverModalLayout).CallDeferred();
        }
        catch (Exception ex)
        {
            ModLogger.Error("Secret 输入弹窗布局初始化失败。", ex, assembly);
            Complete(null);
            Close();
            return;
        }

        Callable.From(() => input?.GrabFocus()).CallDeferred();
    }

    public override void _ExitTree()
    {
        Complete(null);
        base._ExitTree();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Cancel();
            GetViewport()?.SetInputAsHandled();
        }
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

    internal void PrepareForOpen()
    {
        EnsureLayoutBuilt();
        RecoverModalLayout();
    }

    private void BuildLayout()
    {
        Name = "JmcSecretInputPopup";
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = PopupZIndex;
        FillParent(this);

        var center = new CenterContainer();
        FillParent(center);
        AddChild(center);

        panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(
                Math.Max(520f, options.MinimumSize.X),
                Math.Max(300f, options.MinimumSize.Y))
        };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 26);
        margin.AddThemeConstantOverride("margin_top", 22);
        margin.AddThemeConstantOverride("margin_right", 26);
        margin.AddThemeConstantOverride("margin_bottom", 22);
        panel.AddChild(margin);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var title = new Label
        {
            Text = options.Title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        root.AddChild(title);

        if (!string.IsNullOrWhiteSpace(options.Description))
        {
            var description = new RichTextLabel
            {
                BbcodeEnabled = true,
                FitContent = true,
                ScrollActive = false,
                Text = options.Description,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            root.AddChild(description);
        }

        input = new LineEdit
        {
            Secret = true,
            PlaceholderText = options.Placeholder ?? string.Empty,
            CustomMinimumSize = new Vector2(0f, 46f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        input.TextSubmitted += _ => Confirm();
        root.AddChild(input);

        statusLabel = new Label
        {
            Text = string.Empty,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        root.AddChild(statusLabel);

        var footer = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        footer.AddThemeConstantOverride("separation", 12);
        root.AddChild(footer);

        var spacer = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        footer.AddChild(spacer);

        var cancel = new Button
        {
            Text = string.IsNullOrWhiteSpace(options.CancelText) ? "Cancel" : options.CancelText.Trim(),
            CustomMinimumSize = new Vector2(130f, 44f)
        };
        cancel.Pressed += Cancel;
        footer.AddChild(cancel);

        confirmButton = new Button
        {
            Text = string.IsNullOrWhiteSpace(options.ConfirmText) ? "Save" : options.ConfirmText.Trim(),
            CustomMinimumSize = new Vector2(130f, 44f)
        };
        confirmButton.Pressed += Confirm;
        footer.AddChild(confirmButton);
    }

    internal void RecoverModalLayout()
    {
        Vector2 viewportSize = GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
        FillParent(this);
        if (viewportSize.X > 0f && viewportSize.Y > 0f)
        {
            Size = viewportSize;
        }

        if (panel != null)
        {
            panel.CustomMinimumSize = ResolvePanelMinimumSize(viewportSize);
        }

        Node? parent = GetParent();
        if (parent != null)
        {
            parent.MoveChild(this, Math.Max(0, parent.GetChildCount() - 1));
        }
    }

    private void Confirm()
    {
        string value = input?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            if (statusLabel != null)
            {
                statusLabel.Text = options.EmptyText ?? "Secret value cannot be empty.";
            }

            return;
        }

        Complete(value);
        Close();
    }

    private void Cancel()
    {
        Complete(null);
        Close();
    }

    private void Close()
    {
        NModalContainer? modalContainer = NModalContainer.Instance;
        if (modalContainer != null && ReferenceEquals(modalContainer.OpenModal, this))
        {
            modalContainer.Clear();
            return;
        }

        QueueFree();
    }

    private void Complete(string? value)
    {
        if (completed)
        {
            return;
        }

        completed = true;
        completion.TrySetResult(value);
    }

    private static void FillParent(Control control)
    {
        control.SetAnchorsPreset(LayoutPreset.FullRect);
        control.OffsetLeft = 0f;
        control.OffsetTop = 0f;
        control.OffsetRight = 0f;
        control.OffsetBottom = 0f;
    }

    private Vector2 ResolvePanelMinimumSize(Vector2 viewportSize)
    {
        Vector2 configured = new(
            Math.Max(520f, options.MinimumSize.X),
            Math.Max(300f, options.MinimumSize.Y));

        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            return configured;
        }

        return new Vector2(
            Math.Min(configured.X, Math.Max(360f, viewportSize.X * 0.88f)),
            Math.Min(configured.Y, Math.Max(260f, viewportSize.Y * 0.82f)));
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
}
