using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Instrumentation;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.Library;
using UIExtenderLib;
using UIExtenderLib.ViewModel;
using Debug = System.Diagnostics.Debug;

namespace UIExtenderLibModule.ViewModel
{
    internal class ViewModelComponent
    {
        private readonly Dictionary<Type, List<Type>> _mixins = new Dictionary<Type, List<Type>>();
        private readonly Dictionary<Type, Type> _extendedTypeCache = new Dictionary<Type, Type>();
        private readonly Dictionary<Type, IViewModelMixin> _mixinInstanceCache = new Dictionary<Type, IViewModelMixin>();
        private readonly Dictionary<Type, object> _extendedTypeInstanceCache = new Dictionary<Type, object>();

        private readonly AssemblyBuilder _assemblyBuilder;
        private readonly ModuleBuilder _moduleBuilder;

        internal ViewModelComponent()
        {
            var assembly = new AssemblyName("UIExtenderVMAssembly");
            _assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assembly, AssemblyBuilderAccess.Run | AssemblyBuilderAccess.Save);
            _moduleBuilder = _assemblyBuilder.DefineDynamicModule("UIExtenderVMAssemblyModule", "UIExtender_VMGeneratedAssemblyModule.dll");
        }

        /**
         * Register mixin type.
         * Type should be a subclass of ViewModelMixin<T> with T specifying what
         * view model this mixin extends.
         */
        internal void RegisterViewModelMixin(Type mixinType)
        {
            Type viewModelType = null;
            var node = mixinType;
            while (node != null)
            {
                if (typeof(IViewModelMixin).IsAssignableFrom(node))
                {
                    viewModelType = node.GetGenericArguments().FirstOrDefault();
                    if (viewModelType != null)
                    {
                        break;
                    }
                }

                node = node.BaseType;
            }

            Debug.Assert(viewModelType != null, $"Failed to find base type for mixin {mixinType}, should be specialized as T of ViewModelMixin<T>!");
            _mixins.Get(viewModelType, () => new List<Type>()).Add(mixinType);
        }

        /**
         * Check if any mixins are registered for type
         */
        internal bool ExtendsViewModelType(Type t)
        {
            return _mixins.ContainsKey(t);
        }
        
        /**
         * Get mixin class instance for mixin type.
         * Used to proxy calls to specific mixins in extended VM methods.
         * Requires extended VM constructor to be run first, should only be used internally.
         */
        internal IViewModelMixin MixinInstanceForType(Type t)
        {
            Debug.Assert(_mixinInstanceCache.ContainsKey(t), $"Mixin instance is not (yet) created for type {t}");
            return _mixinInstanceCache[t];
        }
        
        /**
         * Get extended VM type for view model type.
         * Generates it if it's not found in cache.
         */
        internal Type ExtendedViewModelForType(Type t)
        {
            return _extendedTypeCache.Get(t, () => GenerateExtendedVMTypeFor(t));
        }

        /**
         * Initialize mixin instances for view model type.
         * Will instantiate registered mixins for that type, called in
         * extended VM constructor after base constructor.
         */
        internal void InitializeMixinsForViewModelInstance(Type t, object instance)
        {
            foreach (var mixinType in _mixins[t])
            {
                _mixinInstanceCache[mixinType] = (IViewModelMixin)Activator.CreateInstance(mixinType, new [] { instance });
            }
        }

        /**
         * Call `Refresh` on mixins for view model types.
         */
        internal void RefreshMixinsForTypes(Type[] types)
        {
            foreach (Type t in types)
            {
                if (_mixins.ContainsKey(t))
                {
                    foreach (var mixinType in _mixins[t])
                    {
                        IViewModelMixin mixinInstance;
                        if (_mixinInstanceCache.TryGetValue(mixinType, out mixinInstance))
                        {
                            mixinInstance.Refresh();
                        }
                    }
                }
            }
        }

        /**
         * Generate type in the shared _assemblyBuilder for view model type
         */
        private Type GenerateExtendedVMTypeFor(Type t)
        {
            Debug.Assert(_mixins.ContainsKey(t), $"Invalid extended vm generation call - type {t} is not registered!");
            
            var builder = _moduleBuilder.DefineType("UIExtenderLib_" + t.Name, TypeAttributes.Class | TypeAttributes.Public, t);

            {
                // constructor
                var defaultConstructor = t.GetConstructors().First();
                var constructorSignature = defaultConstructor.GetParameters().Select(p => p.ParameterType).ToArray();

                var constructor = builder.DefineConstructor(
                    MethodAttributes.Public,
                    CallingConventions.Standard,
                    constructorSignature
                );

                {
                    var gen = constructor.GetILGenerator();

                    // call base constructor
                    for (int i = 0; i < constructorSignature.Length + 1; i++)
                    {
                        gen.Emit(OpCodes.Ldarg, i);
                    }

                    gen.Emit(OpCodes.Call, defaultConstructor);

                    // instaniate mixins of this type
                    var instantiateMixins = typeof(ViewModelPatchUtil).GetMethod(nameof(ViewModelPatchUtil.InitializeMixinsForModel));
                    var getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle");
                    gen.Emit(OpCodes.Ldtoken, t);
                    gen.Emit(OpCodes.Call, getTypeFromHandle);
                    gen.Emit(OpCodes.Ldarg_0);
                    gen.Emit(OpCodes.Call, instantiateMixins);

                    gen.Emit(OpCodes.Ret);
                }
            }

            foreach (var mixin in _mixins[t])
            {
                // properties
                foreach (var property in mixin.GetProperties().Where(p => p.CustomAttributes.Any(a => a.AttributeType == typeof(DataSourceProperty))))
                {
                    var newProperty = builder.DefineProperty(property.Name, PropertyAttributes.None, property.PropertyType, new Type[] {});
                    var attributeConstructor = typeof(DataSourceProperty).GetConstructor(new Type[] { });
                    var customBuilder = new CustomAttributeBuilder(attributeConstructor, new object[] {});
                    newProperty.SetCustomAttribute(customBuilder);
                    
                    // getter
                    MethodAttributes getSetAttr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
                    var newMethod = builder.DefineMethod("get_" + property.Name, getSetAttr, property.PropertyType, Type.EmptyTypes);
                    var gen = newMethod.GetILGenerator();
                    
                    // body
                    var getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle");
                    var getInstance = typeof(ViewModelPatchUtil).GetMethod(nameof(ViewModelPatchUtil.GetMixinInstanceForType));
                    var method = property.GetGetMethod();
                    
                    gen.Emit(OpCodes.Ldtoken, mixin);
                    gen.Emit(OpCodes.Call, getTypeFromHandle);
                    gen.Emit(OpCodes.Call, getInstance);
                    gen.Emit(OpCodes.Call, method);
                    gen.Emit(OpCodes.Ret);
                    
                    newProperty.SetGetMethod(newMethod);
                }

                // methods
                foreach (var method in mixin.GetMethods().Where(m => m.CustomAttributes.Any(a => a.AttributeType == typeof(DataSourceMethod))))
                {
                    var newMethod = builder.DefineMethod(method.Name, MethodAttributes.Public, null, new Type[] { });
                    var gen = newMethod.GetILGenerator();

                    // body
                    var getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle");
                    var getInstance = typeof(ViewModelPatchUtil).GetMethod(nameof(ViewModelPatchUtil.GetMixinInstanceForType));
                    
                    gen.Emit(OpCodes.Ldtoken, mixin);
                    gen.Emit(OpCodes.Call, getTypeFromHandle);
                    gen.Emit(OpCodes.Call, getInstance);
                    gen.Emit(OpCodes.Call, method);
                    gen.Emit(OpCodes.Ret);
                }
            }
            
            return builder.CreateType();
        }

        /**
         * Save generated assembly to the `bin` folder of the game. It's not actually loaded
         * and only used to troubleshoot generated code.
         */
        internal void SaveRuntimeImages()
        {
            _assemblyBuilder.Save("UIExtender_VMGeneratedAssemblyModule.runtime_dll");
        }
    }
}