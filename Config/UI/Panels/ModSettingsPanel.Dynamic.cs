using JmcModLib.Config.Entry;

namespace JmcModLib.Config.UI;

internal sealed partial class ModSettingsPanel
{
    private void RegisterDynamicDropdown(DynamicDropdownBinding binding)
    {
        if (binding.Entry.DropdownOptionsProviderAttribute == null)
        {
            return;
        }

        dynamicDropdowns[binding.Key] = binding;

        foreach (ConfigEntry dependency in DropdownOptionsResolver.ResolveDependencyEntries(binding.Entry))
        {
            string dependencyKey = CreateBindingKey(dependency);
            if (!dynamicDropdownDependents.TryGetValue(dependencyKey, out List<string>? dependents))
            {
                dependents = [];
                dynamicDropdownDependents[dependencyKey] = dependents;
            }

            if (!dependents.Contains(binding.Key, StringComparer.Ordinal))
            {
                dependents.Add(binding.Key);
            }
        }
    }

    private void RefreshDynamicDropdownDependents(ConfigEntry changedEntry)
    {
        string changedKey = CreateBindingKey(changedEntry);
        if (!dynamicDropdownDependents.TryGetValue(changedKey, out List<string>? dependents))
        {
            return;
        }

        foreach (string dependentKey in dependents.ToArray())
        {
            if (dynamicDropdowns.TryGetValue(dependentKey, out DynamicDropdownBinding? binding))
            {
                RefreshDynamicDropdown(binding);
            }
        }
    }

    private void RefreshDynamicDropdown(DynamicDropdownBinding binding)
    {
        if (!refreshingDynamicDropdowns.Add(binding.Key))
        {
            return;
        }

        try
        {
            IReadOnlyList<string> options = ResolveDropdownOptionsForUi(
                binding.Entry,
                binding.DropdownAttribute,
                binding.ValueType);
            binding.State.Options = options;
            binding.ApplyOptions(options, binding.Entry.GetValue());
        }
        finally
        {
            refreshingDynamicDropdowns.Remove(binding.Key);
        }
    }

    private IReadOnlyList<string> ResolveDropdownOptionsForUi(
        ConfigEntry entry,
        UIDropdownAttribute? dropdownAttribute,
        Type valueType)
    {
        return DropdownOptionsResolver.ResolveEffective(entry, dropdownAttribute, valueType, TrySetEntryValue);
    }
}
