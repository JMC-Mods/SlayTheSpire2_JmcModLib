using Godot;
using JmcModLib.Prefabs;
using JmcModLib.Security;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;

namespace JmcModLib.Config.UI;

internal sealed partial class ModSettingsPanel
{
    private const float SecretStatusWidth = 270f;
    private const float SecretButtonWidth = 170f;

    private Control BuildSecretEditor(SecretEntry entry, List<Control> focusableControls)
    {
        var wrapper = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(620f, 0f),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        wrapper.AddThemeConstantOverride("separation", 10);

        MegaRichTextLabel status = CreateStyledText(BuildSecretStatusText(entry));
        status.CustomMinimumSize = new Vector2(SecretStatusWidth, 0f);
        status.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        status.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        wrapper.AddChild(status);

        Control setButton = BuildCompactActionButton(
            ConfigLocalization.GetSecretSetButtonText(entry, ModSettingsText.SecretSetButton()),
            SecretButtonWidth,
            () => TaskHelper.RunSafely(OpenSecretInputAsync(entry, status)),
            UIButtonColor.Green);
        wrapper.AddChild(setButton);
        focusableControls.Add(setButton);

        Control clearButton = BuildCompactActionButton(
            ConfigLocalization.GetSecretClearButtonText(entry, ModSettingsText.SecretClearButton()),
            SecretButtonWidth,
            () => TaskHelper.RunSafely(ClearSecretAsync(entry, status)),
            UIButtonColor.Reset);
        wrapper.AddChild(clearButton);
        focusableControls.Add(clearButton);

        return wrapper;
    }

    private async Task OpenSecretInputAsync(SecretEntry entry, MegaRichTextLabel statusLabel)
    {
        JmcSecretProtectionLevel protectionLevel = entry.Slot.ProtectionLevel;
        if (protectionLevel == JmcSecretProtectionLevel.Unavailable)
        {
            await JmcConfirmationPopup.ShowMessageAsync(
                ModSettingsText.SecretInputUnavailableTitle(),
                ModSettingsText.SecretInputUnavailableBody(),
                ModSettingsText.Close(),
                assembly: entry.Assembly);
            RefreshSecretStatusLabel(entry, statusLabel);
            return;
        }

        string? value = await JmcSecretInputPopup.PromptAsync(
            new JmcSecretInputPopupOptions
            {
                Title = ConfigLocalization.GetDisplayName(entry),
                Description = BuildSecretPromptDescription(entry, protectionLevel),
                Placeholder = ModSettingsText.SecretInputPlaceholder(),
                ConfirmText = ModSettingsText.SecretInputConfirm(),
                CancelText = ModSettingsText.SecretInputCancel(),
                EmptyText = ModSettingsText.SecretInputEmpty(),
                ProtectionLevel = protectionLevel
            },
            entry.Assembly);

        if (value == null)
        {
            RefreshSecretStatusLabel(entry, statusLabel);
            return;
        }

        if (!entry.TrySave(value, out JmcSecretWriteStatus status))
        {
            await JmcConfirmationPopup.ShowMessageAsync(
                ModSettingsText.SecretInputUnavailableTitle(),
                ModSettingsText.SecretSaveFailed(FormatSecretWriteStatus(status)),
                ModSettingsText.Close(),
                assembly: entry.Assembly);
        }

        RefreshSecretStatusLabel(entry, statusLabel);
    }

    private async Task ClearSecretAsync(SecretEntry entry, MegaRichTextLabel statusLabel)
    {
        bool confirmed = await JmcConfirmationPopup.ShowConfirmationAsync(
            ModSettingsText.SecretClearTitle(),
            ModSettingsText.SecretClearBody(ConfigLocalization.GetDisplayName(entry)),
            ModSettingsText.SecretClearButton(),
            ModSettingsText.SecretInputCancel(),
            assembly: entry.Assembly);
        if (!confirmed)
        {
            RefreshSecretStatusLabel(entry, statusLabel);
            return;
        }

        if (!entry.TryDelete(out JmcSecretWriteStatus status))
        {
            await JmcConfirmationPopup.ShowMessageAsync(
                ModSettingsText.SecretClearTitle(),
                ModSettingsText.SecretClearFailed(FormatSecretWriteStatus(status)),
                ModSettingsText.Close(),
                assembly: entry.Assembly);
        }

        RefreshSecretStatusLabel(entry, statusLabel);
    }

    private static string BuildSecretPromptDescription(SecretEntry entry, JmcSecretProtectionLevel protectionLevel)
    {
        string description = ConfigLocalization.GetDescription(entry);
        if (protectionLevel != JmcSecretProtectionLevel.WeakFileProtection)
        {
            return description;
        }

        string warning = $"[color=#e0b24f]{ModSettingsText.SecretInputWeakWarning()}[/color]";
        return string.IsNullOrWhiteSpace(description)
            ? warning
            : $"{description}\n{warning}";
    }

    private static string BuildSecretStatusText(SecretEntry entry)
    {
        return entry.Slot.ProtectionLevel switch
        {
            JmcSecretProtectionLevel.Unavailable =>
                $"[color=#d07f7f]{ModSettingsText.SecretStatusUnavailable()}[/color]",
            JmcSecretProtectionLevel.WeakFileProtection when entry.Exists() =>
                $"[color=#e0b24f]{ModSettingsText.SecretStatusSavedWeak()}[/color]",
            JmcSecretProtectionLevel.WeakFileProtection =>
                $"[color=#e0b24f]{ModSettingsText.SecretStatusWeak()}[/color]",
            _ when entry.Exists() =>
                $"[color=#7ee787]{ModSettingsText.SecretStatusSaved()}[/color]",
            _ =>
                $"[color=#aab7bc]{ModSettingsText.SecretStatusMissing()}[/color]"
        };
    }

    private static string FormatSecretWriteStatus(JmcSecretWriteStatus status)
    {
        return status switch
        {
            JmcSecretWriteStatus.Success => ModSettingsText.SecretStatusSaved(),
            JmcSecretWriteStatus.Unavailable => ModSettingsText.SecretStatusUnavailable(),
            JmcSecretWriteStatus.AccessDenied => ModSettingsText.SecretStatusAccessDenied(),
            JmcSecretWriteStatus.WeakProtectionNotAllowed => ModSettingsText.SecretStatusWeakNotAllowed(),
            _ => ModSettingsText.SecretStatusBackendError()
        };
    }

    private static void RefreshSecretStatusLabel(SecretEntry entry, MegaRichTextLabel statusLabel)
    {
        if (!IsGodotObjectValid(statusLabel))
        {
            return;
        }

        statusLabel.Text = BuildSecretStatusText(entry);
    }
}
