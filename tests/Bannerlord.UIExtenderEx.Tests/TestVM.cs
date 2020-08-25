using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests
{
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
}