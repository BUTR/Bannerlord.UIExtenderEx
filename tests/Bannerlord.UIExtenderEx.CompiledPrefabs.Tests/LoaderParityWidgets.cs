using System.Xml;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Represents a test widget accepting a prefab <c>&lt;CustomElements&gt;</c> node, which the XML loader passes to properties of type <see cref="XmlElement"/>.
/// </summary>
public class CustomElementWidget : Widget
{
    public CustomElementWidget(UIContext context) : base(context) { }

    public XmlElement? Custom { get; set; }
}

/// <summary>
/// Represents a test widget whose dotted attribute paths resolve only against runtime instances due to <c>Holder</c> being declared as <see cref="object"/>.
/// </summary>
public class LooseHolderWidget : Widget
{
    public LooseHolderWidget(UIContext context) : base(context) { }

    public object Holder { get; } = new LooseHolder();
}

public class LooseHolder
{
    public float Value { get; set; }
}

/// <summary>
/// Replicates re-entrant setter cascades seen in GauntletUI <c>NumericUpDownWidget</c>, where <c>Value</c> updates <c>IntValue</c>
/// and re-triggers property change notifications before the outer setter completes.
/// </summary>
public class ReenteringValueWidget : Widget
{
    public ReenteringValueWidget(UIContext context) : base(context) { }

    private float _value;
    private int _intValue;

    public float Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                IntValue = (int) _value;
                OnPropertyChanged(value, nameof(Value));
            }
        }
    }

    public int IntValue
    {
        get => _intValue;
        set
        {
            if (_intValue != value)
            {
                _intValue = value;
                Value = _intValue;
                OnPropertyChanged(value, nameof(IntValue));
            }
        }
    }
}

/// <summary>Represents a lightweight test widget providing a string property without requiring UI context font resolution.</summary>
public class LabelWidget : Widget
{
    public LabelWidget(UIContext context) : base(context) { }

    public string? Label { get; set; } = "unset";
}

/// <summary>
/// Tracks property assignment frequency internally to distinguish retained widgets from re-instantiated widgets.
/// </summary>
public class CountingLabelWidget : Widget
{
    public CountingLabelWidget(UIContext context) : base(context) { }

    private int _sets;
    private string? _label = "unset";

    public string? Label
    {
        get => _label;
        set
        {
            _sets++;
            _label = value;
        }
    }
}

/// <summary>
/// Replicates widget property setters that throw exceptions when invoked before child widgets or dependencies are initialized.
/// </summary>
public class FragileSetterWidget : Widget
{
    public FragileSetterWidget(UIContext context) : base(context) { }

    public string? Fragile
    {
        get => null;
        set => throw new System.InvalidOperationException("the setter ran into state the widget does not have yet");
    }

    public string? After { get; set; } = "unset";
}

/// <summary>Represents a test widget holding a widget reference property resolved via relative or absolute paths.</summary>
public class ReferenceWidget : Widget
{
    public ReferenceWidget(UIContext context) : base(context) { }

    public Widget? Target { get; set; }
}

/// <summary>
/// Replicates widget behavior that fires UI events (<c>Opened</c>) during state assignment, verifying event suppression during initial binding.
/// </summary>
public class OpenedOnSetWidget : Widget
{
    public OpenedOnSetWidget(UIContext context) : base(context) { }

    private string? _state;

    public string? State
    {
        get => _state;
        set
        {
            _state = value;
            EventFired("Opened");
        }
    }
}

/// <summary>
/// Replicates widgets that subscribe internally to their own events prior to view handler registration, updating state before commands fire.
/// </summary>
public class SelfListeningWidget : Widget
{
    public SelfListeningWidget(UIContext context) : base(context) { }

    private int _clicks;

    public bool ListensToItself
    {
        get => false;
        set
        {
            if (value)
                EventFire += OnOwnEvent;
        }
    }

    public int Clicks
    {
        get => _clicks;
        set
        {
            if (_clicks != value)
            {
                _clicks = value;
                OnPropertyChanged(value, nameof(Clicks));
            }
        }
    }

    private void OnOwnEvent(Widget widget, string eventName, object[] args)
    {
        if (eventName == "Click")
            Clicks++;
    }
}
