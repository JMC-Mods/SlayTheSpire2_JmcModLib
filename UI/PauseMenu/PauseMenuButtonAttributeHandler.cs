using JmcModLib.Core.AttributeRouter;
using JmcModLib.Reflection;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace JmcModLib.UI.PauseMenu;

internal sealed class PauseMenuButtonAttributeHandler : IAttributeHandler
{
    public Action<Assembly, IReadOnlyList<ReflectionAccessorBase>>? Unregister => null;

    public void Handle(Assembly assembly, ReflectionAccessorBase accessor, Attribute attribute)
    {
        if (accessor is not MethodAccessor method || attribute is not PauseMenuButtonAttribute buttonAttribute)
        {
            return;
        }

        try
        {
            PauseMenuButtonOptions options = BuildOptions(method.MemberInfo, buttonAttribute);
            Func<PauseMenuButtonContext, Task> action = BuildAction(method.MemberInfo);
            PauseMenuRegistry.RegisterInternal(assembly, options, action);
            ModLogger.Trace($"Registered pause menu button {options.Key}", assembly);
        }
        catch (Exception ex)
        {
            ModLogger.Error(
                $"Failed to register pause menu button for {method.DeclaringType.FullName}.{method.Name}",
                ex,
                assembly);
        }
    }

    private static PauseMenuButtonOptions BuildOptions(MethodInfo method, PauseMenuButtonAttribute attribute)
    {
        string key = string.IsNullOrWhiteSpace(attribute.Key)
            ? $"{method.DeclaringType?.FullName}.{method.Name}"
            : attribute.Key.Trim();

        return new PauseMenuButtonOptions(key, attribute.Text)
        {
            Anchor = attribute.Anchor,
            Order = attribute.Order,
            LocTable = attribute.LocTable,
            TextKey = attribute.TextKey,
            CloseMenuOnClick = attribute.CloseMenuOnClick,
            Color = attribute.Color
        };
    }

    private static Func<PauseMenuButtonContext, Task> BuildAction(MethodInfo method)
    {
        ValidateMethod(method);
        ParameterInfo[] parameters = method.GetParameters();
        bool hasContext = parameters.Length == 1;
        bool returnsTask = typeof(Task).IsAssignableFrom(method.ReturnType);

        return context =>
        {
            object? result = Invoke(method, hasContext ? [context] : []);
            return returnsTask
                ? (Task)(result ?? Task.CompletedTask)
                : Task.CompletedTask;
        };
    }

    private static void ValidateMethod(MethodInfo method)
    {
        if (!method.IsStatic)
        {
            throw new ArgumentException("Pause menu button method must be static.");
        }

        if (method.ContainsGenericParameters)
        {
            throw new ArgumentException("Pause menu button method must not contain generic parameters.");
        }

        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length > 1)
        {
            throw new ArgumentException(
                $"Pause menu button method must have zero or one parameter, but {parameters.Length} were found.");
        }

        if (parameters.Length == 1 && parameters[0].ParameterType != typeof(PauseMenuButtonContext))
        {
            throw new ArgumentException(
                $"Pause menu button parameter must be {typeof(PauseMenuButtonContext).FullName}.");
        }

        if (method.ReturnType != typeof(void) && !typeof(Task).IsAssignableFrom(method.ReturnType))
        {
            throw new ArgumentException("Pause menu button method must return void or Task.");
        }
    }

    private static object? Invoke(MethodInfo method, object?[] args)
    {
        try
        {
            return method.Invoke(null, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
