using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using UIExtenderLib;
using UIExtenderLib.Interface;

namespace AnotherExampleModule
{
    [PrefabExtension("MapBar", "descendant::ListPanel[@Id='BottomInfoBar']/Children")]
    public class BottomBarExtension : PrefabExtensionInsertPatch
    {
        public override int Position => PositionLast;
        public override string Name => "AnotherExampleExtension";
    }

    [ViewModelMixin]
    public class MapInfoMixin : BaseViewModelMixin<MapInfoVM>
    {
        private string _hintText;
        
        [DataSourceProperty] public BasicTooltipViewModel AnotherExampleExtensionHint => new BasicTooltipViewModel(() => _hintText);

        public MapInfoMixin(MapInfoVM vm) : base(vm)
        {
        }
        
        public override void OnRefresh()
        {
            _hintText = "AnotherExampleExtension Tick " + Utilities.GetDeltaTime(0);
        }
    }
    
    [ViewModelMixin]
    public class MapInfoAdditionalMixin : BaseViewModelMixin<MapInfoVM>
    {
        [DataSourceProperty] public string AnotherExampleExtensionValue => "AEE";

        public MapInfoAdditionalMixin(MapInfoVM vm) : base(vm)
        {
        }
    }

    public class SubModule: MBSubModuleBase
    {
        private UIExtender _uiExtender = new UIExtender("AnotherExampleModule");
        
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            _uiExtender.Register();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            
            _uiExtender.Verify();
        }
    }
}