using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;
using Bannerlord.UIExtenderEx.ViewModels;

using NUnit.Framework;

using System;
using System.Collections.Generic;

using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Tests dynamic member resolution for polymorphic ViewModel slots where child bindings reference members
/// specific to only one derived sibling type or its mixin.
/// <para>
/// Simulates scenarios like Diplomacy's diplomacy panel where a polymorphic base slot (<c>KingdomDiplomacyItemVM</c>)
/// holds either war (<c>KingdomWarItemVM</c>) or truce (<c>KingdomTruceItemVM</c>) instances. When prefab bindings target
/// members unique to one sibling (or its associated mixin), the compiled runtime reads null and logs diagnostic miss messages
/// without breaking bindings or throwing unhandled exceptions.
/// </para>
/// </summary>
[NonParallelizable]
public class SiblingMixinMemberTests
{
    private const string Movie = "SiblingMixinMovie";
    private const string Prefab = """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget Id="Item" DataSource="{Selected}">
          <Children>
            <Widget Id="Trade" IsVisible="@HasTradeAgreement" />
            <Widget Id="TradeEnd" HoveredCursorState="@TradeAgreementEndTimeStr" />
            <Widget Id="PactPanel" IsVisible="@IsPactVisible">
              <Children>
                <Widget Id="PactName" HoveredCursorState="@PactActionName" />
              </Children>
            </Widget>
          </Children>
        </Widget>
      </Children>
    </Widget>
  </Window>
</Prefab>
""";

    private UIExtender _extender = null!;
    private PrefabWorkspace _workspace = null!;
    private TestUIContext _ui = null!;
    private RecordingHost _host = null!;
    private IDynamicMemberHost? _previousHost;

    [SetUp]
    public void SetUp()
    {
        _previousHost = DynamicMember.Host;
        DynamicMember.Host = _host = new RecordingHost();
        DynamicMember.Reset();
        _extender = UIExtender.Create("TestModule.CompiledPrefabs." + nameof(SiblingMixinMemberTests) + "." + NUnit.Framework.TestContext.CurrentContext.Test.Name);
        _extender.Register([typeof(SiblingWarItemMixin), typeof(SiblingTruceItemMixin)]);
        _extender.Enable();
        _workspace = new PrefabWorkspace((Movie, Prefab));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(SiblingMixinMemberTests));
    }

    [TearDown]
    public void TearDown()
    {
        _extender?.Deregister();
        _ui?.Dispose();
        _workspace?.Dispose();
        DynamicMember.Host = _previousHost;
        DynamicMember.Reset();
    }

    private Widget XmlRoot(ViewModel viewModel)
    {
        var loaded = GauntletMovie.Load(_ui.Context, _workspace.WidgetFactory, Movie, viewModel, doNotUseGeneratedPrefabs: true, hotReloadEnabled: false);
        Assert.That(loaded, Is.InstanceOf<GauntletMovie>(), "the XML path was not taken");
        return loaded.RootWidget;
    }

    private static Widget ById(Widget root, string id) => root.FindChild(id, includeAllChildren: true) ?? throw new InvalidOperationException($"No widget '{id}'.");

    /// <summary>
    /// Verifies that each sibling ViewModel type receives its designated mixin instance exclusively.
    /// </summary>
    [Test]
    public void EachSiblingCarriesItsOwnMixin()
    {
        var war = new SiblingWarItemVM();
        var truce = new SiblingTruceItemVM();

        Assert.That(ViewModelMixins.Get<SiblingWarItemMixin>(war), Is.Not.Null);
        Assert.That(ViewModelMixins.Get<SiblingTruceItemMixin>(truce), Is.Not.Null);
        Assert.That(war.GetPropertyValue("IsPactVisible"), Is.EqualTo(false));
        Assert.That(war.GetPropertyValue("PactActionName"), Is.Null, "the truce's mixin member");
        Assert.That(war.GetPropertyValue("HasTradeAgreement"), Is.Null, "the truce's own member");
        Assert.That(truce.GetPropertyValue("PactActionName"), Is.EqualTo("pact"));
    }

    /// <summary>
    /// Verifies that selecting the war sibling evaluates truce-specific members as null across XML and compiled movies,
    /// recording diagnostic miss reports for the missing members on the war ViewModel.
    /// </summary>
    [Test]
    public void AWarSelected_ReadsTheTrucesNamesAsNull_AsTheXmlLoaderDoes_AndReportsThem()
    {
        var xmlRoot = XmlRoot(new SiblingHostVM(new SiblingWarItemVM()));
        var compiled = CompiledMovie.Build(_workspace, Movie, typeof(SiblingHostVM), _ui);
        compiled.SetDataSource(new SiblingHostVM(new SiblingWarItemVM()));

        foreach (var root in new[] { xmlRoot, compiled.Root })
        {
            Assert.That(ById(root, "Trade").IsVisible, Is.False, "null into the bool setter");
            Assert.That(ById(root, "TradeEnd").HoveredCursorState, Is.Null);
            Assert.That(ById(root, "PactPanel").IsVisible, Is.False, "the war's mixin answers this one");
            Assert.That(ById(root, "PactName").HoveredCursorState, Is.Null);
        }

        Assert.That(_host.Messages, Is.EquivalentTo(new[]
        {
            Miss("HasTradeAgreement", typeof(SiblingWarItemVM)),
            Miss("TradeAgreementEndTimeStr", typeof(SiblingWarItemVM)),
            Miss("PactActionName", typeof(SiblingWarItemVM)),
        }));
    }

    /// <summary>
    /// Verifies that switching the active selection to the truce sibling resolves all truce-specific and mixin members dynamically.
    /// </summary>
    [Test]
    public void SelectingTheTruce_ResolvesItsMembersAndItsMixins_AsTheXmlLoaderDoes()
    {
        var xmlViewModel = new SiblingHostVM(new SiblingWarItemVM());
        var xmlRoot = XmlRoot(xmlViewModel);
        var compiled = CompiledMovie.Build(_workspace, Movie, typeof(SiblingHostVM), _ui);
        var compiledViewModel = new SiblingHostVM(new SiblingWarItemVM());
        compiled.SetDataSource(compiledViewModel);

        xmlViewModel.Selected = new SiblingTruceItemVM();
        compiledViewModel.Selected = new SiblingTruceItemVM();

        foreach (var root in new[] { xmlRoot, compiled.Root })
        {
            Assert.That(ById(root, "Trade").IsVisible, Is.True);
            Assert.That(ById(root, "TradeEnd").HoveredCursorState, Is.EqualTo("in 20 days"));
            Assert.That(ById(root, "PactPanel").IsVisible, Is.True);
            Assert.That(ById(root, "PactName").HoveredCursorState, Is.EqualTo("pact"));
        }
        Assert.That(_host.Messages, Has.None.Contains(typeof(SiblingTruceItemVM).FullName!), "nothing the truce is asked for is missing");

        // Resets selection back to the war item to verify bindings reset cleanly.
        xmlViewModel.Selected = new SiblingWarItemVM();
        compiledViewModel.Selected = new SiblingWarItemVM();
        foreach (var root in new[] { xmlRoot, compiled.Root })
        {
            Assert.That(ById(root, "Trade").IsVisible, Is.False);
            Assert.That(ById(root, "PactName").HoveredCursorState, Is.Null);
        }
    }

    private static string Miss(string name, Type runtimeType) =>
        $"UIExtenderEx: compiled prefab binding '{name}' (Read) found no member on {runtimeType.FullName}.";

    private sealed class RecordingHost : IDynamicMemberHost
    {
        private readonly DynamicMemberHost _inner = new();

        public List<string> Messages { get; } = [];

        public bool HasProperty(ViewModel target, string name) => _inner.HasProperty(target, name);

        public bool HasMethod(ViewModel target, string name) => _inner.HasMethod(target, name);

        public bool TrySetProperty(ViewModel target, string name, object? value) => _inner.TrySetProperty(target, name, value);

        public void Report(string message) => Messages.Add(message);
    }
}

/// <summary>
/// Test host ViewModel representing a container with a polymorphic sibling item selection slot.
/// </summary>
public class SiblingHostVM : ViewModel
{
    private SiblingItemBaseVM _selected;

    public SiblingHostVM(SiblingItemBaseVM selected) => _selected = selected;

    [DataSourceProperty]
    public SiblingItemBaseVM Selected
    {
        get => _selected;
        set
        {
            if (value != _selected)
            {
                _selected = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }
}

/// <summary>
/// Abstract base ViewModel representing shared sibling item functionality.
/// </summary>
public abstract class SiblingItemBaseVM : ViewModel { }

/// <summary>
/// Concrete sibling ViewModel representing war items lacking truce-specific properties.
/// </summary>
public class SiblingWarItemVM : SiblingItemBaseVM { }

/// <summary>
/// Concrete sibling ViewModel providing truce-specific trade agreement properties.
/// </summary>
public class SiblingTruceItemVM : SiblingItemBaseVM
{
    [DataSourceProperty]
    public bool HasTradeAgreement => true;

    [DataSourceProperty]
    public string TradeAgreementEndTimeStr => "in 20 days";
}

/// <summary>
/// Mixin attached to war items configuring pact visibility without pact-specific action properties.
/// </summary>
[ViewModelMixin]
public class SiblingWarItemMixin : BaseViewModelMixin<SiblingWarItemVM>
{
    public SiblingWarItemMixin(SiblingWarItemVM vm) : base(vm) { }

    [DataSourceProperty]
    public bool IsPactVisible => false;
}

/// <summary>
/// Mixin attached to truce items providing pact visibility and action name properties.
/// </summary>
[ViewModelMixin]
public class SiblingTruceItemMixin : BaseViewModelMixin<SiblingTruceItemVM>
{
    public SiblingTruceItemMixin(SiblingTruceItemVM vm) : base(vm) { }

    [DataSourceProperty]
    public bool IsPactVisible => true;

    [DataSourceProperty]
    public string PactActionName => "pact";
}
