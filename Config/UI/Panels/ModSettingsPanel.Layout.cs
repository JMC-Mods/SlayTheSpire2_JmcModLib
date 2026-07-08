using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace JmcModLib.Config.UI;

internal sealed partial class ModSettingsPanel
{
    private void BuildLayout()
    {
        this.SetAnchorsPreset(LayoutPreset.TopLeft);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;

        centerRoot = new CenterContainer
        {
            Name = "CenterRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        AddChild(centerRoot);

        root = new VBoxContainer
        {
            Name = "VBoxContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(PreferredContentWidth, 0f)
        };
        root.AddThemeConstantOverride("separation", 14);
        centerRoot.AddChild(root);

        var titleRow = new HBoxContainer
        {
            Name = "TitleRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        titleRow.AddThemeConstantOverride("separation", 20);

        titleLabel = CreateStyledText($"[gold]{ModSettingsText.Title()}[/gold]");
        Control title = titleLabel;
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        titleRow.AddChild(title);

        titleActions = new HBoxContainer
        {
            Name = "TitleActions",
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        titleActions.AddThemeConstantOverride("separation", 12);
        titleRow.AddChild(titleActions);
        root.AddChild(titleRow);

        descriptionLabel = CreateDescriptionText($"[color=#aab7bc]{ModSettingsText.Description()}[/color]");
        root.AddChild(descriptionLabel);

        root.AddChild(new HSeparator());

        listRoot = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        listRoot.AddThemeConstantOverride("separation", 16);
        root.AddChild(listRoot);
    }

    private MegaRichTextLabel CreateStyledText(string text)
    {
        if (nativeTemplates?.RichLabelTemplate != null)
        {
            MegaRichTextLabel label = (MegaRichTextLabel)nativeTemplates.RichLabelTemplate.Duplicate();
            ConfigureWrappedText(label, text);
            return label;
        }

        var fallback = new MegaRichTextLabel
        {
            BbcodeEnabled = true
        };
        ConfigureWrappedText(fallback, text);
        return fallback;
    }

    private MegaRichTextLabel CreateDescriptionText(string text)
    {
        MegaRichTextLabel label;
        if (nativeTemplates?.RichLabelTemplate != null)
        {
            label = (MegaRichTextLabel)nativeTemplates.RichLabelTemplate.Duplicate();
        }
        else
        {
            label = new MegaRichTextLabel
            {
                BbcodeEnabled = true
            };
        }

        ConfigureWrappedText(label, text);
        label.AutoSizeEnabled = false;
        label.MinFontSize = IntroFontSize;
        label.MaxFontSize = IntroFontSize;
        label.Call("SetFontSize", IntroFontSize);
        return label;
    }

    private static void ConfigureWrappedText(MegaRichTextLabel label, string text)
    {
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.FitContent = true;
        label.ScrollActive = false;
        label.AutoSizeEnabled = false;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(0f, label.CustomMinimumSize.Y);
        label.Text = text;
    }

    private void RefreshPanelSize()
    {
        if (root == null || centerRoot == null)
        {
            return;
        }

        Control? parent = GetParent<Control>();
        if (parent == null)
        {
            return;
        }

        LayoutBounds bounds = ResolveContentBounds(parent);
        float positionX = bounds.Left;
        float width = bounds.Width;

        root.CustomMinimumSize = new Vector2(width, 0f);
        root.Size = new Vector2(width, root.Size.Y);
        centerRoot.CustomMinimumSize = new Vector2(width, 0f);
        ApplyResponsiveEditorWidths(width);

        Vector2 minimumSize = root.GetMinimumSize();
        float height = MathF.Max(minimumSize.Y, 1f);
        Size = new Vector2(width, height);
        centerRoot.Size = Size;
        root.Size = new Vector2(width, height);
        Position = new Vector2(positionX, Position.Y);
    }

    internal void SetLayoutReference(Control templatePanel)
    {
        layoutReferencePanel = templatePanel;
        RefreshPanelSizeAfterLayout();
    }

    private LayoutBounds ResolveContentBounds(Control parent)
    {
        if (TryResolveReferenceBounds(parent, out LayoutBounds referenceBounds))
        {
            return referenceBounds;
        }

        float parentWidth = MathF.Max(parent.Size.X, 1f);
        float rightLimit = ResolveRightLimit(parent, parentWidth);
        float width = MathF.Min(PreferredContentWidth, rightLimit);
        float left = MathF.Max((rightLimit - width) * 0.5f, 0f);
        return new LayoutBounds(left, width);
    }

    private bool TryResolveReferenceBounds(Control parent, out LayoutBounds bounds)
    {
        bounds = default;
        Control? reference = layoutReferencePanel;
        if (reference == null
            || !IsGodotObjectValid(reference)
            || !reference.IsInsideTree()
            || !parent.IsInsideTree())
        {
            return false;
        }

        Control content = reference.GetNodeOrNull<Control>("VBoxContainer") ?? reference;
        float left = content.GlobalPosition.X - parent.GlobalPosition.X;
        float width = MathF.Max(content.Size.X, content.GetMinimumSize().X);
        if (width <= 1f)
        {
            width = MathF.Max(reference.Size.X, reference.GetMinimumSize().X);
        }

        float parentWidth = MathF.Max(parent.Size.X, 1f);
        if (!float.IsFinite(left) || !float.IsFinite(width) || width <= 1f || left >= parentWidth)
        {
            return false;
        }

        left = MathF.Max(left, 0f);
        float rightLimit = ResolveRightLimit(parent, parentWidth);
        width = MathF.Min(width, PreferredContentWidth);
        width = MathF.Min(width, MathF.Max(rightLimit - left, 1f));
        bounds = new LayoutBounds(left, width);
        return true;
    }

    private float ResolveRightLimit(Control parent, float fallback)
    {
        float rightLimit = fallback;
        if (TryResolveScrollbarLeft(parent, out float scrollbarLeft))
        {
            rightLimit = MathF.Min(rightLimit, scrollbarLeft - ScrollbarContentGap);
        }

        return MathF.Max(rightLimit, 1f);
    }

    private bool TryResolveScrollbarLeft(Control parent, out float scrollbarLeft)
    {
        scrollbarLeft = 0f;
        NScrollableContainer? scrollContainer = FindAncestor<NScrollableContainer>();
        NScrollbar? scrollbar = scrollContainer?.Scrollbar;
        if (scrollbar == null
            || !IsGodotObjectValid(scrollbar)
            || !parent.IsInsideTree()
            || !scrollbar.IsInsideTree()
            || !scrollbar.Visible)
        {
            return false;
        }

        scrollbarLeft = scrollbar.GlobalPosition.X - parent.GlobalPosition.X;
        return float.IsFinite(scrollbarLeft) && scrollbarLeft > 1f;
    }

    private void ApplyResponsiveEditorWidths(float contentWidth)
    {
        if (root == null)
        {
            return;
        }

        foreach (JmcKeybindButton keybind in FindDescendants<JmcKeybindButton>(root))
        {
            keybind.SetResponsiveWidth(ResolveAvailableWidthInRow(keybind, contentWidth));
        }
    }

    private static float ResolveAvailableWidthInRow(Control control, float contentWidth)
    {
        if (control.GetParent() is not HBoxContainer row)
        {
            return contentWidth;
        }

        float reservedWidth = 0f;
        int childCount = 0;
        foreach (Node child in row.GetChildren())
        {
            if (child is not Control sibling)
            {
                continue;
            }

            childCount++;
            if (sibling != control)
            {
                reservedWidth += MathF.Max(sibling.CustomMinimumSize.X, sibling.GetMinimumSize().X);
            }
        }

        float separation = row.GetThemeConstant("separation");
        reservedWidth += MathF.Max(childCount - 1, 0) * separation;
        return MathF.Max(contentWidth - reservedWidth, 1f);
    }

    private static IEnumerable<T> FindDescendants<T>(Node rootNode) where T : Node
    {
        foreach (Node child in rootNode.GetChildren())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (T nested in FindDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private T? FindAncestor<T>() where T : Node
    {
        Node? current = this;
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current.GetParent();
        }

        return null;
    }

    private readonly record struct LayoutBounds(float Left, float Width);

    private void RefreshPanelSizeAfterLayout()
    {
        RefreshPanelSize();
        Callable.From(RefreshPanelSize).CallDeferred();
        Callable.From(() => Callable.From(RefreshPanelSize).CallDeferred()).CallDeferred();
    }

    private void UpdateFocusMap(List<Control> focusableControls)
    {
        if (focusableControls.Count == 0)
        {
            _firstControl = null;
            return;
        }

        _firstControl = focusableControls[0];

        for (int i = 0; i < focusableControls.Count; i++)
        {
            Control current = focusableControls[i];
            Control previous = i > 0 ? focusableControls[i - 1] : current;
            Control next = i < focusableControls.Count - 1 ? focusableControls[i + 1] : current;

            current.FocusMode = FocusModeEnum.All;
            current.FocusNeighborLeft = current.GetPath();
            current.FocusNeighborRight = current.GetPath();
            current.FocusNeighborTop = previous.GetPath();
            current.FocusNeighborBottom = next.GetPath();
        }
    }
}
