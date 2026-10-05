using System;
using System.Diagnostics.CodeAnalysis;

namespace Bannerlord.UIExtenderEx.Attributes;

/// <summary>
/// Serves as the abstract base class for all UIExtenderEx extension attributes.
/// </summary>
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
[SuppressMessage("Design", "RCS1203:Use AttributeUsageAttribute.", Justification = "Implemented in the derived attributes.")]
public abstract class BaseUIExtenderAttribute : Attribute;