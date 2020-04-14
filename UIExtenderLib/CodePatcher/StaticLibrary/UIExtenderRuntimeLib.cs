using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;
using TaleWorlds.Library;

namespace UIExtenderLib.CodePatcher.StaticLibrary
{
    /// <summary>
    /// Library of general functions that are called in generated IL code (in order for it to be more simplistic).
    /// Most of this methods are simple proxy-calls to methods further down in hierarchy. Most are called in generated extended VM classes.
    /// </summary>
    public static class UIExtenderRuntimeLib
    {
        /// <summary>
        /// Proxy method to ViewModelComponent.MixinInstanceForObject
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="mixinType"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static object MixinInstanceForVMInstance(string moduleName, Type mixinType, object instance)
        {
            return UIExtender.RuntimeFor(moduleName).ViewModelComponent.MixinInstanceForVMInstance(mixinType, instance);
        }

        /// <summary>
        /// Proxy method to ViewModelComponent.InitializeMixinsForViewModelInstance
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="t"></param>
        /// <param name="instance"></param>
        public static void InitializeMixinsForVMInstance(string moduleName, Type t, object instance)
        {
            var runtime = UIExtender.RuntimeFor(moduleName);
            
            // Verify should have been called at this point
            Utils.SoftAssert(runtime.VerifyCalled, $"UIExtender.Verify was not called!");

            runtime.ViewModelComponent.InitializeMixinsForVMInstance(t, instance);
        }

        /// <summary>
        /// Proxy method to ViewModelComponent.DestructMixinsForInstance
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="instance"></param>
        public static void DestructMixinsForVMInstance(string moduleName, object instance)
        {
            UIExtender.RuntimeFor(moduleName).ViewModelComponent.DestructMixinsForVMInstance(instance);
        }

        /// <summary>
        /// Proxy method to ViewModelComponent.FinalizeMixinsForInstance
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="instance"></param>
        public static void FinalizeMixinsForVMInstance(string moduleName, object instance)
        {
            UIExtender.RuntimeFor(moduleName).ViewModelComponent.FinalizeMixinForVMInstance(instance);
        }

        /// <summary>
        /// Load XmlDocument with file at path. 
        /// Equivalent XML code from `WidgetPrefab.LoadFrom`, which is called from patch.
        /// Original loading code was removed in order to make space for patch instructions.
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="path"></param>
        /// <param name="document"></param>
        public static void LoadXmlDocument(string moduleName, string path, XmlDocument document)
        {
            using (XmlReader xmlReader = XmlReader.Create(path, new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
            }))
            {
                document.Load(xmlReader);
            }
        }

        /// <summary>
        /// Method inserted into `WidgetFactory.LoadFrom`, which patches XmlDocument.
        /// A number of those are run in sequence for each extension.
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="path"></param>
        /// <param name="document"></param>
        public static void ProcessMovieDocumentIfNeeded(string moduleName, string path, XmlDocument document)
        {
            // currently replicates exactly what WidgetFactory is doing
            var movieName = Path.GetFileNameWithoutExtension(path);
            
            // proxy call to ProcessMovieIfNeeded
            UIExtender.RuntimeFor(moduleName).PrefabComponent.ProcessMovieIfNeeded(movieName, document);
        }

        /// <summary>
        /// Method that recursively searches for MethodInfo of method named `name`. Used in `ViewModel.ExecuteCommand` in order
        /// for it to recognize base class methods.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="name"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public static MethodInfo FindExecuteCommandTargetMethod(Type t, string name, BindingFlags flags)
        {
            if (t == null)
            {
                return null;
            }
			
            var method = t.GetMethod(name, flags | BindingFlags.FlattenHierarchy);
            return method != null ? method : FindExecuteCommandTargetMethod(t.BaseType, name, flags);
        }
    }
}