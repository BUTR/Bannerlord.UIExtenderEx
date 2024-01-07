using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests;

public class TestVM : ViewModel
{
    [DataSourceProperty]
    public object TestProperty => null!;
    public object TestMethod() => null!;

    public override void OnFinalize()
    {
        base.OnFinalize();
    }
}

public class DerivedTestVM : TestVM
{
    [DataSourceProperty]
    public object Test2Property => null!;
    public object Test2Method() => null!;

    public override void OnFinalize()
    {
        base.OnFinalize();
    }
}