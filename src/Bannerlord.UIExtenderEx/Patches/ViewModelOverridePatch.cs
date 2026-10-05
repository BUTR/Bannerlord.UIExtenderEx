using Bannerlord.UIExtenderEx.Utils;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Patches;

/// <summary>
/// Installs Harmony prefixes on host ViewModel methods intercepted by <see cref="BUTRViewModelOverrideAttribute"/> mixin overrides.
/// <para>
/// A shared Harmony prefix intercepts target method invocations, assembles an execution chain of enabled overrides in registration
/// order, and invokes the initial override. Each override receives an <c>original</c> delegate to invoke the next override or the
/// original host implementation. If an instance possesses no enabled overrides for the method, the prefix bypasses interception
/// and executes natively.
/// </para>
/// </summary>
internal static class ViewModelOverridePatch
{
    private static readonly ConcurrentDictionary<MethodBase, bool> Patched = new();
    private static readonly ConcurrentDictionary<MethodBase, Action<object, object?[]>> Invokers = new();
    private static readonly ConcurrentDictionary<Type, Func<Continuation, Delegate>> DelegateFactories = new();

    /// <summary>Tracks the active instance and method designated for one-shot pass-through execution when the terminal override invokes <c>original</c>.</summary>
    [ThreadStatic]
    private static (object Instance, MethodBase Method)? _passThrough;

    /// <summary>Installs the shared interception prefix on the specified host ViewModel method.</summary>
    public static void Patch(Harmony harmony, MethodInfo method)
    {
        if (!Patched.TryAdd(method, true))
            return;
        if (!harmony.TryPatch(method, prefix: AccessTools2.DeclaredMethod(typeof(ViewModelOverridePatch), nameof(Prefix))))
            MessageUtils.DisplayUserWarning("Failed to patch {0}.{1}! What mods change about it will not happen.", method.DeclaringType!.FullName!, method.Name);
    }

    private static bool Prefix(object __instance, MethodBase __originalMethod, object?[] __args)
    {
        if (_passThrough is { } pass && ReferenceEquals(pass.Instance, __instance) && pass.Method == __originalMethod)
        {
            _passThrough = null;
            return true;
        }
        if (__instance is not ViewModel viewModel)
            return true;

        var chain = CollectChain(viewModel, __originalMethod);
        if (chain is null)
            return true;

        new Continuation(viewModel, __originalMethod, chain, 0).Run(__args);
        return false;
    }

    private static List<(object Mixin, MethodInfo Method)>? CollectChain(ViewModel viewModel, MethodBase method)
    {
        List<(object, MethodInfo)>? chain = null;
        foreach (var runtime in UIExtender.GetAllRuntimes())
        {
            if (!runtime.ViewModelComponent.MixinInstanceCache.TryGetValue(viewModel, out var mixins))
                continue;
            foreach (var mixin in mixins)
            {
                if (runtime.ViewModelComponent.TryGetOverride(mixin.GetType(), method, out var overrideMethod))
                    (chain ??= []).Add((mixin, overrideMethod));
            }
        }
        return chain;
    }

    /// <summary>One link of a chain: what an <c>original</c> delegate does when it is called.</summary>
    internal sealed class Continuation
    {
        private readonly ViewModel _viewModel;
        private readonly MethodBase _method;
        private readonly List<(object Mixin, MethodInfo Method)> _chain;
        private readonly int _index;

        public Continuation(ViewModel viewModel, MethodBase method, List<(object Mixin, MethodInfo Method)> chain, int index)
        {
            _viewModel = viewModel;
            _method = method;
            _chain = chain;
            _index = index;
        }

        /// <summary>The override at this link with the arguments and the next link as its original, or past the last one the body.</summary>
        public void Run(object?[] arguments)
        {
            if (_index >= _chain.Count)
            {
                RunBody(arguments);
                return;
            }

            var (mixin, method) = _chain[_index];
            var withOriginal = new object?[arguments.Length + 1];
            Array.Copy(arguments, withOriginal, arguments.Length);
            var originalType = method.GetParameters()[arguments.Length].ParameterType;
            withOriginal[arguments.Length] = DelegateFactories.GetOrAdd(originalType, CreateDelegateFactory)(new Continuation(_viewModel, _method, _chain, _index + 1));
            try
            {
                method.Invoke(mixin, withOriginal);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is { } inner)
            {
                ExceptionDispatchInfo.Capture(inner).Throw();
            }
        }

        private void RunBody(object?[] arguments)
        {
            _passThrough = (_viewModel, _method);
            try
            {
                Invokers.GetOrAdd(_method, CreateInvoker)(_viewModel, arguments);
            }
            finally
            {
                // Whatever the prefix did not take; a method nothing patches any more leaves it here
                _passThrough = null;
            }
        }
    }

    /// <summary>
    /// A call of the patched method, not of what it overrides or of an override below it: mixins are attached to instances
    /// of exactly the type the method was resolved for, so a virtual call lands on the same declaration.
    /// Not a <c>DynamicMethod</c> with a non-virtual call: <c>DynamicMethod</c> is not in netstandard2.0's compile surface,
    /// and the package that adds it would flow into every mod.
    /// </summary>
    private static Action<object, object?[]> CreateInvoker(MethodBase method)
    {
        var handler = MethodInvoker.GetHandler((MethodInfo) method);
        return (instance, arguments) => handler(instance, arguments);
    }

    /// <summary>An <c>original</c> of the delegate type the override declares, calling <see cref="Continuation.Run"/> with its arguments.</summary>
    private static Func<Continuation, Delegate> CreateDelegateFactory(Type delegateType)
    {
        var invoke = delegateType.GetMethod("Invoke")!;
        var continuation = Expression.Parameter(typeof(Continuation), "continuation");
        var parameters = invoke.GetParameters().Select(x => Expression.Parameter(x.ParameterType, x.Name)).ToArray();
        var run = Expression.Call(continuation, typeof(Continuation).GetMethod(nameof(Continuation.Run))!,
            Expression.NewArrayInit(typeof(object), parameters.Select(x => Expression.Convert(x, typeof(object)))));
        return Expression.Lambda<Func<Continuation, Delegate>>(Expression.Lambda(delegateType, run, parameters), continuation).Compile();
    }
}