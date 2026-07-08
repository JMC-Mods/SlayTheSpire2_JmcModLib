using Godot;
using JmcModLib.Config.Entry;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace JmcModLib.Config.UI;

internal sealed partial class ModSettingsPanel : NSettingsPanel
{

    private readonly Dictionary<string, Action<object?>> bindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DynamicDropdownBinding> dynamicDropdowns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> dynamicDropdownDependents = new(StringComparer.Ordinal);
    private readonly HashSet<string> refreshingDynamicDropdowns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DynamicVisibilityBinding> dynamicVisibilityBindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> dynamicVisibilityDependents = new(StringComparer.Ordinal);

    private const float PreferredContentWidth = 1120f;
    private const float ScrollbarContentGap = 40f;
    private const int IntroFontSize = 24;
    private const float CollapseButtonWidth = 240f;
    private const float GlobalButtonWidth = 260f;
    private const float ActionButtonHeight = 56f;
    private const float KeybindEnableToggleWidth = 64f;

    private CenterContainer? centerRoot;
    private VBoxContainer? root;
    private VBoxContainer? listRoot;
    private HBoxContainer? titleActions;
    private MegaRichTextLabel? titleLabel;
    private MegaRichTextLabel? descriptionLabel;
    private SettingsUiTemplates? nativeTemplates;
    private JmcKeybindButton? listeningKeybind;
    private Viewport? connectedViewport;
    private Callable? viewportSizeChangedCallable;
    private Control? layoutReferencePanel;
    private bool suppressControlEvents;
    private bool restartRequiredConfigChanged;

    private sealed class DropdownEditorState(IReadOnlyList<string> options)
    {
        public IReadOnlyList<string> Options { get; set; } = options;
    }

    private sealed class DynamicDropdownBinding(
        string key,
        ConfigEntry entry,
        UIDropdownAttribute? dropdownAttribute,
        Type valueType,
        DropdownEditorState state,
        Action<IReadOnlyList<string>, object?> applyOptions)
    {
        public string Key { get; } = key;

        public ConfigEntry Entry { get; } = entry;

        public UIDropdownAttribute? DropdownAttribute { get; } = dropdownAttribute;

        public Type ValueType { get; } = valueType;

        public DropdownEditorState State { get; } = state;

        public Action<IReadOnlyList<string>, object?> ApplyOptions { get; } = applyOptions;
    }

    private sealed class DynamicVisibilityBinding(string key, ConfigEntry entry, Control target)
    {
        public string Key { get; } = key;

        public ConfigEntry Entry { get; } = entry;

        public Control Target { get; } = target;
    }

    public static ModSettingsPanel Create()
    {
        return new ModSettingsPanel
        {
            Name = "JmcModLibModSettingsPanel",
            Visible = false
        };
    }
}
