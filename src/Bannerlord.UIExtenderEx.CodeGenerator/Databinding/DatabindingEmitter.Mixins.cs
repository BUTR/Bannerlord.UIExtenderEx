using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;

using TaleWorlds.Library.CodeGeneration;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;

/// <summary>
/// Emits infrastructure for resolving and accessing ViewModel mixin members attached to data sources.
/// </summary>
internal sealed partial class DatabindingEmitter
{
    /// <summary>Contains comment text preceding fallback branches where instances lack the required mixin and fall back to ViewModel members.</summary>
    private const string WithoutMixinComment = "//No mixin on this instance: the ViewModel's own member of this name, by name, as the XML loader does";

    /// <summary>
    /// Maps each unique <c>(OwnerVariable, MixinType)</c> pair to a receiver index, collected prior to code generation to ensure clear routines can reset them.
    /// </summary>
    private readonly Dictionary<(string OwnerVariable, Type MixinType), int> _mixinReceivers = new();

    /// <summary>
    /// Identifies all <c>(OwnerVariable, MixinType)</c> combinations required by property bindings, commands, and child data sources across the prefab.
    /// </summary>
    private void CollectMixinReceivers()
    {
        foreach (var scope in _scopes)
        {
            var ownerVariable = scope.FieldName;
            var ownerType = GetTypeAtPath(scope.Path);
            foreach (var child in scope.Children)
            {
                if (ViewModelMemberResolution.GetProperty(ownerType, child.Path.LastNode, out var mixinType) is not null && mixinType != null)
                {
                    AddMixinReceiver(ownerVariable, mixinType);
                }
            }
            foreach (var binding in scope.Widgets)
            {
                foreach (var propertyBinding in binding.PropertyBindings.Values)
                {
                    if (propertyBinding.MixinType is { } mixinType)
                    {
                        AddMixinReceiver(ownerVariable, mixinType);
                    }
                }
                foreach (var commandBinding in binding.CommandBindings.Values)
                {
                    // Owners not tracked by fields evaluate dynamically by name at runtime.
                    if (commandBinding.MixinType is { } mixinType && TryGetScope(binding.GetCommandOwnerPath(commandBinding)) is { } commandOwner)
                    {
                        AddMixinReceiver(commandOwner.FieldName, mixinType);
                    }
                }
            }
        }

        void AddMixinReceiver(string ownerVariable, Type mixinType)
        {
            if (!_mixinReceivers.ContainsKey((ownerVariable, mixinType)))
            {
                _mixinReceivers.Add((ownerVariable, mixinType), _mixinReceivers.Count);
            }
        }
    }

    /// <summary>
    /// Generates caching fields and accessor methods for each required mixin receiver.
    /// <para>
    /// Caches mixin references per data source field to avoid repeated table lookups. Resets cached references when the underlying
    /// data source changes or is cleared, preventing memory leaks.
    /// </para>
    /// </summary>
    private void CreateMixinReceivers(ClassCode classCode)
    {
        foreach (var ((ownerVariable, mixinType), index) in _mixinReceivers)
        {
            var mixinTypeName = ViewModelMemberResolution.GetCodeTypeName(mixinType);
            classCode.AddVariable(new VariableCode
            {
                Name = MixinOwnerField(index),
                AccessModifier = VariableCodeAccessModifier.Private,
                Type = "global::TaleWorlds.Library.ViewModel",
            });
            classCode.AddVariable(new VariableCode
            {
                Name = MixinField(index),
                AccessModifier = VariableCodeAccessModifier.Private,
                Type = mixinTypeName,
            });
            var methodCode = new MethodCode
            {
                Name = MixinReceiverMethod(index),
                ReturnParameter = mixinTypeName,
                AccessModifier = MethodCodeAccessModifier.Private,
            };
            methodCode.AddLine($"//Mixin {mixinType.FullName} of {ownerVariable}, looked up once per data source");
            methodCode.AddBlock($"if (!object.ReferenceEquals({ownerVariable}, {MixinOwnerField(index)}))", () =>
            {
                methodCode.AddLine($"{MixinOwnerField(index)} = {ownerVariable};");
                methodCode.AddLine($"{MixinField(index)} = {ViewModelMemberResolution.GetMixinAccessExpression(mixinType, ownerVariable)};");
            });
            methodCode.AddLine($"return {MixinField(index)};");
            classCode.AddMethod(methodCode);
        }
    }

    private string MixinReceiver(string ownerVariable, Type mixinType) => _mixinReceivers.TryGetValue((ownerVariable, mixinType), out var index)
        ? $"{MixinReceiverMethod(index)}()"
        : throw new InvalidOperationException($"Prefab '{_class.PrefabName}' reaches mixin {mixinType.FullName} through '{ownerVariable}', which was not collected as a mixin receiver.");

    private IEnumerable<int> MixinReceiversOf(string ownerVariable) =>
        _mixinReceivers.Where(x => x.Key.OwnerVariable == ownerVariable).Select(x => x.Value);

    private static string MixinOwnerField(int index) => $"_mixinOwner_{index}";
    private static string MixinField(int index) => $"_mixin_{index}";
    private static string MixinReceiverMethod(int index) => $"GetMixin_{index}";

    /// <summary>
    /// Emits code accessing a member on either the target data source or its associated mixin receiver.
    /// <para>
    /// Direct ViewModel members bind directly to the owner variable. Mixin members invoke the cached mixin receiver, apply null guards,
    /// and optionally execute a fallback action (<paramref name="withoutMixin"/>) if the mixin is absent.
    /// </para>
    /// </summary>
    private void EmitAgainstReceiver(MethodCode methodCode, Type? mixinType, string ownerVariable, string memberKind, bool inOwnScope, Action<string> emit,
        Action? withoutMixin = null)
    {
        if (mixinType == null)
        {
            emit(ownerVariable);
            return;
        }
        methodCode.AddLine($"//Mixin {memberKind} from {mixinType.FullName}");
        if (inOwnScope)
        {
            methodCode.AddBlock(null, Guarded);
        }
        else
        {
            Guarded();
        }

        void Guarded()
        {
            methodCode.AddLine($"var mixin = {MixinReceiver(ownerVariable, mixinType)};");
            methodCode.AddBlock("if (mixin != null)", () => emit("mixin"));
            if (withoutMixin != null)
            {
                methodCode.AddBlock("else", withoutMixin);
            }
        }
    }
}