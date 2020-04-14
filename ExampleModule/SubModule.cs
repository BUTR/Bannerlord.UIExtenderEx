using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.Multiplayer;
using UIExtenderLib;
using UIExtenderLib.Interface;
using Debug = System.Diagnostics.Debug;

namespace ExampleModule
{
    [PrefabExtension("MapBar", "descendant::ListPanel[@Id='BottomInfoBar']/Children")]
    public class BottomBarExtension : PrefabExtensionInsertPatch
    {
        public override int Position => PositionLast;
        public override string Name => "ExampleExtension";
    }

    [ViewModelMixin]
    public class MapInfoMixin : BaseViewModelMixin<MapInfoVM>
    {
        private string _hintText;
        
        [DataSourceProperty] public BasicTooltipViewModel ExampleExtensionHint => new BasicTooltipViewModel(() => _hintText);
        [DataSourceProperty] public string ExampleExtensionValue => "EE";

        public MapInfoMixin(MapInfoVM vm) : base(vm)
        {
        }
        
        public override void OnRefresh()
        {
            _hintText = "ExampleExtension Tick " + Utilities.GetDeltaTime(0);
            if (_vm.TryGetTarget(out var vm))
            {
                vm.OnPropertyChanged();
            }
        }
    }
    
    public class SubModule: MBSubModuleBase
    {
        private UIExtender _uiExtender = new UIExtender("ExampleModule");
        
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