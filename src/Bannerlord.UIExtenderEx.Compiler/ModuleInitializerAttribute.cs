#if NETFRAMEWORK
namespace System.Runtime.CompilerServices;

/// <summary>Supplies the <see cref="ModuleInitializerAttribute"/> for .NET Framework targets where it is absent from reference assemblies.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class ModuleInitializerAttribute : Attribute;
#endif