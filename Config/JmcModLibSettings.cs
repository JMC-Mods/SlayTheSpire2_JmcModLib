using JmcModLib.Config.UI;

namespace JmcModLib.Config;

internal static class JmcModLibSettings
{
    internal const string InternalGroup = "jmc_mod_lib";

    [UIToggle]
    [Config(
        "自动在模组管理器显示重启按钮",
        group: InternalGroup,
        Key = "ui.auto_mod_manager_restart_button",
        LocTable = L10n.DefaultTable,
        DisplayNameKey = "EXTENSION.JMCMODLIB.UI.AUTO_MOD_MANAGER_RESTART_BUTTON.NAME",
        DescriptionKey = "EXTENSION.JMCMODLIB.UI.AUTO_MOD_MANAGER_RESTART_BUTTON.DESCRIPTION",
        GroupKey = "EXTENSION.JMCMODLIB.UI.GROUP.JMCMODLIB",
        Order = 10)]
    internal static bool AutoShowModManagerRestartButton = true;
}
