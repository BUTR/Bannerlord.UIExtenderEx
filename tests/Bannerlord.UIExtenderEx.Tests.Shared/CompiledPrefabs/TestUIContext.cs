using NSubstitute;

using System;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Configures a functional <see cref="UIContext"/> backed by a valid <see cref="ResourceDepot"/> to support widget layout invalidations and event manager dirty tracking.
/// <para>
/// Omits font assets and <c>TextWidget</c> dependencies, substituting two-dimension platform contexts to test databinding workflows without initializing full GPU texture pipelines.
/// </para>
/// </summary>
public sealed class TestUIContext : IDisposable
{
    public ResourceDepot ResourceDepot { get; }
    public SpriteData SpriteData { get; }
    public BrushFactory BrushFactory { get; }
    public UIContext Context { get; }

    public TestUIContext(ResourceDepot resourceDepot, string name)
    {
        TestInput.EnsureInitialized();
        ResourceDepot = resourceDepot;
        SpriteData = new SpriteData(name);
        var fontFactory = new FontFactory(ResourceDepot);
        BrushFactory = new BrushFactory(ResourceDepot, "Brushes", SpriteData, fontFactory);
        var platform = Substitute.For<ITwoDimensionPlatform>();
        platform.ReferenceHeight.Returns(1080f);
        platform.ReferenceWidth.Returns(1920f);
        var twoDimension = new TwoDimensionContext(platform, Substitute.For<ITwoDimensionResourceContext>(), ResourceDepot);
        Context = new UIContext(twoDimension, Substitute.For<TaleWorlds.InputSystem.IInputContext>(), SpriteData, fontFactory, BrushFactory);
        Context.Initialize();
    }

    public void Dispose() { }
}