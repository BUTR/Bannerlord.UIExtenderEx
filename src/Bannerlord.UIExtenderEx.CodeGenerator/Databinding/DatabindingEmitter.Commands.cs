using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Extensions;

using System;
using System.Collections.Generic;
using System.Reflection;

using TaleWorlds.Library;
using TaleWorlds.Library.CodeGeneration;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;

/// <summary>
/// Emits event listener callbacks for widget commands, replicating the parameter transformation and execution semantics of <c>GauntletView.OnCommand</c> and <c>ViewModel.ExecuteCommand</c>.
/// </summary>
internal sealed partial class DatabindingEmitter
{
    /// <summary>Generates a dedicated event listener method per widget that dispatches command invocations based on command name.</summary>
    private void CreateEventMethods(ClassCode classCode)
    {
        foreach (var scope in _scopes)
        {
            foreach (var binding in scope.Widgets)
            {
                if (binding.CommandBindings.Count == 0)
                {
                    continue;
                }
                var listenerMethod = new MethodCode
                {
                    Name = $"EventListenerOf{binding.Widget.VariableName}",
                    MethodSignature = "(global::TaleWorlds.GauntletUI.BaseTypes.Widget widget, global::System.String commandName, global::System.Object[] args)",
                    AccessModifier = MethodCodeAccessModifier.Private,
                };
                classCode.AddMethod(listenerMethod);
                foreach (var commandBinding in binding.CommandBindings.Values)
                {
                    listenerMethod.AddBlock($"if (commandName == {GeneratedLiteral.Regular(commandBinding.Command)})", () =>
                        AddCommandSection(listenerMethod, commandBinding, binding.GetCommandOwnerPath(commandBinding)));
                }
            }
        }
    }

    /// <summary>
    /// Emits code preparing the command argument array in accordance with <c>GauntletView.OnCommand</c>, unpacking widget arguments
    /// to their underlying data source objects and appending optional constant parameters into a local <c>arguments</c> array.
    /// </summary>
    private static void AddCommandArguments(MethodCode methodCode, CommandBinding commandBinding)
    {
        methodCode.AddLine($"var arguments = new global::System.Object[args.Length{(commandBinding.Parameter is not null ? " + 1" : "")}];");
        methodCode.AddBlock("for (var i = 0; i < args.Length; i++)", () =>
        {
            methodCode.AddLine("var value = args[i];");
            methodCode.AddLine("var valueWidget = value as global::TaleWorlds.GauntletUI.BaseTypes.Widget;");
            methodCode.AddBlock("if (valueWidget != null)", () =>
            {
                methodCode.AddLine("var valueWidgetData = valueWidget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();");
                methodCode.AddLine("value = valueWidgetData == null ? null : valueWidgetData.Data;");
            });
            methodCode.AddLine("arguments[i] = value;");
        });
        if (commandBinding.Parameter is { } parameter)
        {
            // Append constant command parameters as strings to match loader behavior.
            methodCode.AddLine($"//GotParameter {GeneratedLiteral.Comment(parameter)}");
            methodCode.AddLine($"arguments[args.Length] = {GeneratedLiteral.Regular(parameter)};");
        }
    }

    /// <summary>
    /// Emits dynamic command execution via <see cref="DynamicMemberType"/>.
    /// <para>
    /// Invokes commands dynamically by name when static method signatures cannot be bound, relying on <c>ViewModel.ExecuteCommand</c>
    /// semantics to perform runtime method resolution and argument conversion.
    /// </para>
    /// </summary>
    private static void AddCommandByName(MethodCode methodCode, string ownerExpression, string methodName) =>
        methodCode.AddLine($"{DynamicMemberType}.Execute({ownerExpression}, {GeneratedLiteral.Regular(methodName)}, arguments);");

    /// <summary>
    /// Emits command handling logic for a specific widget event binding.
    /// <para>
    /// Dispatches statically resolved methods via strongly typed invocations conforming to <c>ViewModel.ExecuteCommand</c>
    /// parameter rules (see <see cref="AddTypedCommandCall"/>). Falls back to dynamic by-name execution if methods cannot be resolved
    /// or parameter signatures require runtime reflection.
    /// </para>
    /// </summary>
    private void AddCommandSection(MethodCode methodCode, CommandBinding commandBinding, BindingPath ownerPath)
    {
        var methodName = new BindingPath(commandBinding.Path).LastNode;
        if (TryGetScope(ownerPath) is not { } ownerScope)
        {
            methodCode.AddLine("//The command's owner is reached through a data source no field holds; walked and run by name, as the XML loader does");
            AddCommandArguments(methodCode, commandBinding);
            AddCommandByName(methodCode, GetUncollectedOwnerAccess(ownerPath), methodName);
            return;
        }
        if (ownerScope.IsList)
        {
            // Binding list instances do not execute ViewModel commands.
            methodCode.AddLine("//The command's owner is a binding list, which runs no command");
            return;
        }
        var ownerVariable = ownerScope.FieldName;
        if (commandBinding.Method is not { } method || !CanCallTyped(method))
        {
            methodCode.AddLine(commandBinding.Method is null
                ? "//Not declared on the ViewModel type; run by name, as the XML loader does"
                : "//A signature typed code cannot call the way ExecuteCommand does; run by name, as the XML loader does");
            AddCommandArguments(methodCode, commandBinding);
            AddCommandByName(methodCode, ownerVariable, methodName);
            return;
        }

        // Prepare argument arrays only when the target method requires parameters or falls back to ViewModel dynamic dispatch.
        if (commandBinding.ParameterTypes.Length > 0 || commandBinding.FallsBackToViewModel)
        {
            AddCommandArguments(methodCode, commandBinding);
        }
        // Guard against null ViewModel references.
        methodCode.AddBlock($"if ({ownerVariable} != null)", () =>
            EmitAgainstReceiver(methodCode, commandBinding.MixinType, ownerVariable, "command", inOwnScope: false,
                receiver => AddTypedCommandCall(methodCode, commandBinding, receiver, methodName),
                commandBinding.FallsBackToViewModel
                    ? () =>
                    {
                        methodCode.AddLine(WithoutMixinComment);
                        AddCommandByName(methodCode, ownerVariable, methodName);
                    }
        : null));
    }

    /// <summary>
    /// Determines whether a method can be invoked via strongly typed C# code without generic parameter constraints or ref/pointer arguments.
    /// </summary>
    private static bool CanCallTyped(MethodInfo method)
    {
        if (method.ContainsGenericParameters)
        {
            return false;
        }
        foreach (var parameter in method.GetParameters())
        {
            var type = parameter.ParameterType;
            if (type.IsByRef || type.IsPointer || ViewModelMemberResolution.GetCodeTypeName(type) is null)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Emits a strongly typed invocation of the target method against <paramref name="receiver"/>, strictly replicating <c>ViewModel.ExecuteCommand</c> semantics:
    /// <list type="bullet">
    /// <item>Invokes zero-parameter methods regardless of received argument counts.</item>
    /// <item>Verifies argument count equality for parameterized methods.</item>
    /// <item>Converts string arguments targeting numeric parameters (<see cref="int"/>, <see cref="float"/>) using culture-aware parsing.</item>
    /// <item>Verifies argument type compatibility and handles default values for value types.</item>
    /// <item>Wraps invocation exceptions in <see cref="TargetInvocationException"/> in parity with reflection dispatch.</item>
    /// </list>
    /// </summary>
    private static void AddTypedCommandCall(MethodCode methodCode, CommandBinding commandBinding, string receiver, string methodName)
    {
        var parameterTypes = commandBinding.ParameterTypes;
        if (parameterTypes.Length == 0)
        {
            AddProtectedCommandCall(methodCode, $"{receiver}.{GeneratedNaming.Member(methodName)}();");
            return;
        }

        methodCode.AddBlock($"if (arguments.Length == {parameterTypes.Length})", () =>
        {
            var conditions = new List<string>();
            var callArguments = new List<string>();
            for (var i = 0; i < parameterTypes.Length; i++)
            {
                var type = parameterTypes[i];
                var typeName = ViewModelMemberResolution.GetCodeTypeName(type)!;
                var value = $"commandArgument{i}";
                methodCode.AddLine($"var {value} = arguments[{i}];");
                if (type != typeof(string))
                {
                    var conversion = type == typeof(int) ? $"global::System.Convert.ToInt32({value}_text)"
                        : type == typeof(float) ? $"global::System.Convert.ToSingle({value}_text)"
                        : null;
                    methodCode.AddBlock(conversion is null ? $"if ({value} is global::System.String)" : $"if ({value} is global::System.String {value}_text)", () =>
                        methodCode.AddLine($"{value} = {conversion ?? "null"};"));
                }
                if (type != typeof(object))
                {
                    conditions.Add($"({value} == null || {value} is {typeName})");
                }
                callArguments.Add(type.IsValueType && Nullable.GetUnderlyingType(type) is null
                    ? $"({value} == null ? default({typeName}) : ({typeName}){value})"
                    : $"({typeName}){value}");
            }
            var call = $"{receiver}.{GeneratedNaming.Member(methodName)}({string.Join(", ", callArguments)});";
            if (conditions.Count == 0)
            {
                AddProtectedCommandCall(methodCode, call);
            }
            else
            {
                methodCode.AddBlock($"if ({string.Join(" && ", conditions)})", () => AddProtectedCommandCall(methodCode, call));
            }
        });
    }

    /// <summary>Wraps an emitted method call in a try/catch block that bubbles exceptions as <see cref="TargetInvocationException"/>.</summary>
    private static void AddProtectedCommandCall(MethodCode methodCode, string call)
    {
        methodCode.AddBlock("try", () => methodCode.AddLine(call));
        methodCode.AddBlock("catch (global::System.Exception uiExtenderExCommandFailure)", () =>
            methodCode.AddLine("throw new global::System.Reflection.TargetInvocationException(uiExtenderExCommandFailure);"));
    }

    /// <summary>
    /// Generates dynamic navigation expressions for command owners residing at uncollected intermediate paths, walking from the nearest collected ancestor scope in accordance with <c>ViewModel.GetViewModelAtPath</c>.
    /// </summary>
    private string GetUncollectedOwnerAccess(BindingPath ownerPath)
    {
        var remaining = new List<string>();
        for (var path = ownerPath; path is not null; path = path.ParentPath)
        {
            if (TryGetScope(path) is { } scope)
            {
                // Step dynamically along intermediate properties and ensure the terminal target is a ViewModel.
                var expression = $"(object){scope.ObjectAccess}";
                for (var i = remaining.Count - 1; i >= 0; i--)
                {
                    expression = $"{DynamicMemberType}.Step({expression}, {GeneratedLiteral.Regular(remaining[i])})";
                }
                return $"({expression} as global::TaleWorlds.Library.ViewModel)";
            }
            remaining.Add(path.LastNode);
        }
        throw new InvalidOperationException(
            $"The command owner '{ownerPath.Path}' of prefab '{_class.PrefabName}' is not below any data source of the prefab, so its command cannot be generated.");
    }
}