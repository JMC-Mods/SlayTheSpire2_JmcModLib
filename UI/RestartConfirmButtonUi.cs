using Godot;
using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace JmcModLib.UI;

internal static class RestartConfirmButtonUi
{
    private const string ButtonScenePath = "res://scenes/ui/confirm_button.tscn";
    private const string RestartIconPath = "res://JmcModLib/images/ui/restart_button_icon.png";

    private static readonly ConditionalWeakTable<Node, RestartButtonHandle> Handles = new();
    private static readonly Lazy<MemberAccessor?> ControllerHotkeyIconAccessor = new(CreateControllerHotkeyIconAccessor);

    public static void SetVisible(Node owner, bool visible)
    {
        if (!IsValid(owner))
        {
            return;
        }

        if (!visible)
        {
            if (Handles.TryGetValue(owner, out RestartButtonHandle? existing))
            {
                existing.SetVisible(false);
            }

            return;
        }

        RestartButtonHandle? handle = GetOrCreate(owner);
        handle?.SetVisible(true);
    }

    public static void Remove(Node owner)
    {
        if (!Handles.TryGetValue(owner, out RestartButtonHandle? handle))
        {
            return;
        }

        handle.Dispose();
        Handles.Remove(owner);
    }

    private static RestartButtonHandle? GetOrCreate(Node owner)
    {
        if (Handles.TryGetValue(owner, out RestartButtonHandle? existing) && existing.IsValid)
        {
            return existing;
        }

        Handles.Remove(owner);
        RestartButtonHandle? created = RestartButtonHandle.TryCreate(owner);
        if (created == null)
        {
            return null;
        }

        Handles.Add(owner, created);
        return created;
    }

    private static NConfirmButton? CreateButton(Node owner)
    {
        try
        {
            PackedScene? scene = ResourceLoader.Load<PackedScene>(ButtonScenePath, string.Empty, ResourceLoader.CacheMode.Reuse);
            if (scene == null)
            {
                ModLogger.Warn($"无法加载原生重启按钮场景：{ButtonScenePath}");
                return null;
            }

            NConfirmButton button = scene.Instantiate<NConfirmButton>(PackedScene.GenEditState.Disabled);
            button.Name = $"JmcModLibRestartConfirmButton_{owner.GetInstanceId()}";
            return button;
        }
        catch (Exception ex)
        {
            ModLogger.Warn("创建右侧重启按钮失败。", ex);
            return null;
        }
    }

    private static void ConfigureButton(NConfirmButton button)
    {
        button.OverrideHotkeys([]);
        button.Released += _button =>
        {
            _ = GameRestart.ShowRestartConfirmationAsync(assembly: typeof(RestartConfirmButtonUi).Assembly);
        };
        ApplyRestartIcon(button);
        HideControllerHotkeyIcon(button);
    }

    private static void ApplyRestartIcon(NConfirmButton button)
    {
        Texture2D? texture = ResourceLoader.Load<Texture2D>(RestartIconPath, string.Empty, ResourceLoader.CacheMode.Reuse);
        if (texture == null)
        {
            ModLogger.Warn($"无法加载重启按钮图标：{RestartIconPath}");
            return;
        }

        Control? imageRoot = button.GetNodeOrNull<Control>("Image");
        TextureRect? icon = FindRestartIconTarget(button, imageRoot);
        if (icon == null && imageRoot != null)
        {
            icon = CreateRestartIconOverlay(imageRoot);
        }

        if (icon == null)
        {
            ModLogger.Warn("原生确认按钮缺少可替换的图标纹理节点。");
            return;
        }

        foreach (TextureRect nativeIcon in EnumerateReplaceableIconCandidates(button, imageRoot))
        {
            if (nativeIcon.GetInstanceId() != icon.GetInstanceId())
            {
                nativeIcon.Visible = false;
            }
        }

        icon.Texture = texture;
        icon.Visible = true;
        icon.Modulate = Colors.White;
        icon.MouseFilter = Control.MouseFilterEnum.Ignore;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
    }

    private static TextureRect? FindRestartIconTarget(NConfirmButton button, Control? imageRoot)
    {
        List<TextureRect> imageCandidates = imageRoot == null
            ? []
            : EnumerateReplaceableIconCandidates(imageRoot, imageRoot).ToList();
        return SelectPreferredIconCandidate(imageCandidates)
            ?? SelectPreferredIconCandidate(EnumerateReplaceableIconCandidates(button, imageRoot));
    }

    private static TextureRect? SelectPreferredIconCandidate(IEnumerable<TextureRect> candidates)
    {
        return candidates.FirstOrDefault(static candidate =>
            candidate.Name.ToString().Contains("Icon", StringComparison.OrdinalIgnoreCase)
            || candidate.Name.ToString().Contains("Check", StringComparison.OrdinalIgnoreCase)
            || candidate.Name.ToString().Contains("Confirm", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault();
    }

    private static IEnumerable<TextureRect> EnumerateReplaceableIconCandidates(Node root, Control? imageRoot)
    {
        ulong imageRootId = imageRoot?.GetInstanceId() ?? 0;
        foreach (TextureRect textureRect in EnumerateDescendantTextureRects(root))
        {
            if (textureRect.GetInstanceId() == imageRootId)
            {
                continue;
            }

            string name = textureRect.Name.ToString();
            if (name.Equals("Image", StringComparison.Ordinal)
                || name.Equals("Outline", StringComparison.Ordinal)
                || name.Equals("ControllerIcon", StringComparison.Ordinal))
            {
                continue;
            }

            string path = textureRect.GetPath().ToString();
            if (path.Contains("/Outline", StringComparison.Ordinal)
                || path.Contains("/ControllerIcon", StringComparison.Ordinal))
            {
                continue;
            }

            yield return textureRect;
        }
    }

    private static IEnumerable<TextureRect> EnumerateDescendantTextureRects(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is TextureRect textureRect)
            {
                yield return textureRect;
            }

            foreach (TextureRect nested in EnumerateDescendantTextureRects(child))
            {
                yield return nested;
            }
        }
    }

    private static TextureRect CreateRestartIconOverlay(Control imageRoot)
    {
        TextureRect icon = new()
        {
            Name = "JmcModLibRestartIcon",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        imageRoot.AddChild(icon);
        return icon;
    }

    private static void HideControllerHotkeyIcon(NConfirmButton button)
    {
        TextureRect? controllerIcon = button.GetNodeOrNull<TextureRect>("%ControllerIcon");
        if (controllerIcon != null)
        {
            controllerIcon.Visible = false;
        }

        try
        {
            ControllerHotkeyIconAccessor.Value?.SetValue(button, null);
        }
        catch (Exception ex)
        {
            ModLogger.Warn("隐藏重启按钮手柄热键图标失败。", ex);
        }
    }

    private static MemberAccessor? CreateControllerHotkeyIconAccessor()
    {
        try
        {
            return MemberAccessor.Get(typeof(NButton), "_controllerHotkeyIcon");
        }
        catch (Exception ex)
        {
            ModLogger.Warn("无法访问原生按钮手柄热键图标字段。", ex);
            return null;
        }
    }

    private static Node? ResolveButtonParent(Node owner)
    {
        if (NGame.Instance != null && GodotObject.IsInstanceValid(NGame.Instance))
        {
            return NGame.Instance;
        }

        return owner.IsInsideTree()
            ? owner.GetTree().CurrentScene ?? owner
            : owner;
    }

    private static bool IsValid(GodotObject? value)
    {
        return value != null && GodotObject.IsInstanceValid(value);
    }

    private sealed class RestartButtonHandle : IDisposable
    {
        private readonly Node owner;
        private readonly NConfirmButton button;
        private readonly Callable ownerTreeExitingCallable;
        private bool disposed;

        private RestartButtonHandle(Node owner, NConfirmButton button)
        {
            this.owner = owner;
            this.button = button;
            ownerTreeExitingCallable = Callable.From(() => Remove(owner));
            owner.Connect(Node.SignalName.TreeExiting, ownerTreeExitingCallable);
        }

        public bool IsValid => !disposed && RestartConfirmButtonUi.IsValid(button);

        public static RestartButtonHandle? TryCreate(Node owner)
        {
            NConfirmButton? button = CreateButton(owner);
            if (button == null)
            {
                return null;
            }

            Node? parent = ResolveButtonParent(owner);
            if (parent == null || !RestartConfirmButtonUi.IsValid(parent))
            {
                button.QueueFree();
                return null;
            }

            parent.AddChild(button);
            ConfigureButton(button);
            return new RestartButtonHandle(owner, button);
        }

        public void SetVisible(bool visible)
        {
            if (!IsValid)
            {
                return;
            }

            if (visible)
            {
                button.Enable();
            }
            else
            {
                button.Disable();
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                if (RestartConfirmButtonUi.IsValid(owner)
                    && owner.IsConnected(Node.SignalName.TreeExiting, ownerTreeExitingCallable))
                {
                    owner.Disconnect(Node.SignalName.TreeExiting, ownerTreeExitingCallable);
                }
            }
            catch
            {
                // 场景销毁阶段信号连接可能已经失效，忽略即可。
            }

            if (RestartConfirmButtonUi.IsValid(button))
            {
                button.QueueFree();
            }
        }
    }
}
