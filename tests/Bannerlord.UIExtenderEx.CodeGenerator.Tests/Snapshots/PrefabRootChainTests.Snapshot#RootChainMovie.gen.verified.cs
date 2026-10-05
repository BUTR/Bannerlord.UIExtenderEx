namespace Bannerlord.UIExtenderEx.Tests.Generated
{
	//Data: DataSourceType - Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM
	public class RootChainMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM :  global::TaleWorlds.GauntletUI.BaseTypes.Widget,  global::TaleWorlds.GauntletUI.Data.IGeneratedGauntletMovieRoot
	{
		private global::TaleWorlds.GauntletUI.BaseTypes.Widget _widget;
		private RootChainMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_1_RootChainPanel__DependendPrefab _widget_0;
		private global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM _datasource_Root;
		
		public RootChainMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM(global::TaleWorlds.GauntletUI.UIContext context) : base(context)
		{
		}
		
		public void CreateWidgets()
		{
			_widget = this;
			//Requires data component
			{
				if (_widget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget);
					_widget.AddComponent(widgetComponent);
				}
			}
			_widget_0 = new RootChainMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_1_RootChainPanel__DependendPrefab(this.Context);
			//Requires data component
			{
				if (_widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget_0);
					_widget_0.AddComponent(widgetComponent);
				}
			}
			_widget.AddChild(_widget_0);
			_widget_0.CreateWidgets();
		}
		
		public void SetIds()
		{
			this.Id = "RootWidget";
			_widget_0.SetIds();
			_widget_0.Id = "Panel";
		}
		
		public void SetAttributes()
		{
			
			_widget_0.SetAttributes();
			try
			{
				_widget_0.MarginTop = 188f;
			}
			catch (global::System.Exception)
			{
				global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.LoaderAsserts.SetterThrew(_widget_0, "MarginTop", "188");
			}
			
		}
		
		public void RefreshBindingWithChildren()
		{
			var dataSource = _datasource_Root;
			this.SetDataSource(null);
			this.SetDataSource(dataSource);
		}
		
		public void DestroyDataSource()
		{
			if (_datasource_Root != null)
			{
				{
					//Requires component data to be cleared
					var widgetComponent = _widget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				_widget_0.DestroyDataSource();
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				//Binding path: Root
				_datasource_Root.PropertyChanged -= ViewModelPropertyChangedListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithValue -=ViewModelPropertyChangedWithValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithBoolValue -=ViewModelPropertyChangedWithBoolValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithIntValue -=ViewModelPropertyChangedWithIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithFloatValue -=ViewModelPropertyChangedWithFloatValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithUIntValue -=ViewModelPropertyChangedWithUIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithColorValue -=ViewModelPropertyChangedWithColorValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithDoubleValue -=ViewModelPropertyChangedWithDoubleValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithVec2Value -=ViewModelPropertyChangedWithVec2ValueListenerOf_datasource_Root;
				_datasource_Root = null;
			}
		}
		
		public void SetDataSource(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM dataSource)
		{
			RefreshDataSource_datasource_Root(dataSource);
		}
		
		private void ViewModelPropertyChangedListenerOf_datasource_Root(global::System.Object sender, global::System.ComponentModel.PropertyChangedEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithBoolValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithBoolValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithIntValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithIntValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithFloatValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithFloatValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithUIntValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithUIntValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithColorValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithColorValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithDoubleValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithDoubleValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithVec2ValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithVec2ValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void HandleViewModelPropertyChangeOf_datasource_Root(global::System.String propertyName)
		{
			//DataSource property section
			//Primitive property section
		}
		
		private void RefreshDataSource_datasource_Root(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM newDataSource)
		{
			//Clear Section
			if (_datasource_Root != null)
			{
				{
					//Requires component data to be cleared
					var widgetComponent = _widget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				_widget_0.SetDataSource(null);
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				//Binding path: Root
				_datasource_Root.PropertyChanged -= ViewModelPropertyChangedListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithValue -=ViewModelPropertyChangedWithValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithBoolValue -=ViewModelPropertyChangedWithBoolValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithIntValue -=ViewModelPropertyChangedWithIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithFloatValue -=ViewModelPropertyChangedWithFloatValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithUIntValue -=ViewModelPropertyChangedWithUIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithColorValue -=ViewModelPropertyChangedWithColorValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithDoubleValue -=ViewModelPropertyChangedWithDoubleValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithVec2Value -=ViewModelPropertyChangedWithVec2ValueListenerOf_datasource_Root;
				_datasource_Root = null;
			}
			
			_datasource_Root = newDataSource; 
			
			//Assign Section
			if (_datasource_Root != null)
			{
				//Binding path: Root
				_datasource_Root.PropertyChanged += ViewModelPropertyChangedListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithValue +=ViewModelPropertyChangedWithValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithBoolValue +=ViewModelPropertyChangedWithBoolValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithIntValue +=ViewModelPropertyChangedWithIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithFloatValue +=ViewModelPropertyChangedWithFloatValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithUIntValue +=ViewModelPropertyChangedWithUIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithColorValue +=ViewModelPropertyChangedWithColorValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithDoubleValue +=ViewModelPropertyChangedWithDoubleValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithVec2Value +=ViewModelPropertyChangedWithVec2ValueListenerOf_datasource_Root;
				{
					//Requires component data assignment
					var widgetComponent = _widget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				{
					//Requires component data assignment
					var widgetComponent = _widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				_widget_0.SetDataSource(_datasource_Root);
			}
			if (_datasource_Root == null)
			{
				_widget_0.SetDataSource(null);
			}
		}
		
	}
	
	//Data: DataSourceType - Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM
	public class RootChainMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_1_RootChainPanel__DependendPrefab :  RootChainMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_2_RootChainPanelCustom__InheritedPrefab
	{
		private RootChainMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_2_RootChainPanelCustom__InheritedPrefab _widget;
		private global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM _datasource_Root;
		
		public RootChainMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_1_RootChainPanel__DependendPrefab(global::TaleWorlds.GauntletUI.UIContext context) : base(context)
		{
		}
		
		public override void CreateWidgets()
		{
			base.CreateWidgets();
			_widget = this;
			//Requires data component
			{
				if (_widget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget);
					_widget.AddComponent(widgetComponent);
				}
			}
		}
		
		public override void SetIds()
		{
			base.SetIds();
			this.Id = "";
		}
		
		public override void SetAttributes()
		{
			base.SetAttributes();
			
		}
		
		public override void DestroyDataSource()
		{
			base.DestroyDataSource();
			if (_datasource_Root != null)
			{
				{
					//Requires component data to be cleared
					var widgetComponent = _widget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				//Binding path: Root
				_datasource_Root.PropertyChanged -= ViewModelPropertyChangedListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithValue -=ViewModelPropertyChangedWithValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithBoolValue -=ViewModelPropertyChangedWithBoolValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithIntValue -=ViewModelPropertyChangedWithIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithFloatValue -=ViewModelPropertyChangedWithFloatValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithUIntValue -=ViewModelPropertyChangedWithUIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithColorValue -=ViewModelPropertyChangedWithColorValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithDoubleValue -=ViewModelPropertyChangedWithDoubleValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithVec2Value -=ViewModelPropertyChangedWithVec2ValueListenerOf_datasource_Root;
				_datasource_Root = null;
			}
		}
		
		public override void SetDataSource(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM dataSource)
		{
			base.SetDataSource(dataSource);
			RefreshDataSource_datasource_Root(dataSource);
		}
		
		private void ViewModelPropertyChangedListenerOf_datasource_Root(global::System.Object sender, global::System.ComponentModel.PropertyChangedEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithBoolValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithBoolValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithIntValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithIntValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithFloatValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithFloatValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithUIntValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithUIntValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithColorValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithColorValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithDoubleValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithDoubleValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithVec2ValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithVec2ValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void HandleViewModelPropertyChangeOf_datasource_Root(global::System.String propertyName)
		{
			//DataSource property section
			//Primitive property section
		}
		
		private void RefreshDataSource_datasource_Root(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM newDataSource)
		{
			//Clear Section
			if (_datasource_Root != null)
			{
				{
					//Requires component data to be cleared
					var widgetComponent = _widget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				//Binding path: Root
				_datasource_Root.PropertyChanged -= ViewModelPropertyChangedListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithValue -=ViewModelPropertyChangedWithValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithBoolValue -=ViewModelPropertyChangedWithBoolValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithIntValue -=ViewModelPropertyChangedWithIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithFloatValue -=ViewModelPropertyChangedWithFloatValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithUIntValue -=ViewModelPropertyChangedWithUIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithColorValue -=ViewModelPropertyChangedWithColorValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithDoubleValue -=ViewModelPropertyChangedWithDoubleValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithVec2Value -=ViewModelPropertyChangedWithVec2ValueListenerOf_datasource_Root;
				_datasource_Root = null;
			}
			
			_datasource_Root = newDataSource; 
			
			//Assign Section
			if (_datasource_Root != null)
			{
				//Binding path: Root
				_datasource_Root.PropertyChanged += ViewModelPropertyChangedListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithValue +=ViewModelPropertyChangedWithValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithBoolValue +=ViewModelPropertyChangedWithBoolValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithIntValue +=ViewModelPropertyChangedWithIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithFloatValue +=ViewModelPropertyChangedWithFloatValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithUIntValue +=ViewModelPropertyChangedWithUIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithColorValue +=ViewModelPropertyChangedWithColorValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithDoubleValue +=ViewModelPropertyChangedWithDoubleValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithVec2Value +=ViewModelPropertyChangedWithVec2ValueListenerOf_datasource_Root;
				{
					//Requires component data assignment
					var widgetComponent = _widget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
			}
		}
		
	}
	
	//Data: DataSourceType - Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM
	public class RootChainMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_2_RootChainPanelCustom__InheritedPrefab :  global::TaleWorlds.GauntletUI.BaseTypes.Widget
	{
		private global::TaleWorlds.GauntletUI.BaseTypes.Widget _widget;
		private global::TaleWorlds.GauntletUI.BaseTypes.TextWidget _widget_0;
		private global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM _datasource_Root;
		
		public RootChainMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_2_RootChainPanelCustom__InheritedPrefab(global::TaleWorlds.GauntletUI.UIContext context) : base(context)
		{
		}
		
		public virtual void CreateWidgets()
		{
			_widget = this;
			//Requires data component
			{
				if (_widget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget);
					_widget.AddComponent(widgetComponent);
				}
			}
			_widget_0 = new global::TaleWorlds.GauntletUI.BaseTypes.TextWidget(this.Context);
			//Requires data component
			{
				if (_widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget_0);
					_widget_0.AddComponent(widgetComponent);
				}
			}
			_widget.AddChild(_widget_0);
		}
		
		public virtual void SetIds()
		{
			this.Id = "Custom";
			_widget_0.Id = "Label";
		}
		
		public virtual void SetAttributes()
		{
			
			
		}
		
		public virtual void DestroyDataSource()
		{
			if (_datasource_Root != null)
			{
				{
					//Requires component data to be cleared
					var widgetComponent = _widget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				//Binding path: Root
				_datasource_Root.PropertyChanged -= ViewModelPropertyChangedListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithValue -=ViewModelPropertyChangedWithValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithBoolValue -=ViewModelPropertyChangedWithBoolValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithIntValue -=ViewModelPropertyChangedWithIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithFloatValue -=ViewModelPropertyChangedWithFloatValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithUIntValue -=ViewModelPropertyChangedWithUIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithColorValue -=ViewModelPropertyChangedWithColorValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithDoubleValue -=ViewModelPropertyChangedWithDoubleValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithVec2Value -=ViewModelPropertyChangedWithVec2ValueListenerOf_datasource_Root;
				_datasource_Root = null;
			}
		}
		
		public virtual void SetDataSource(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM dataSource)
		{
			RefreshDataSource_datasource_Root(dataSource);
		}
		
		private void ViewModelPropertyChangedListenerOf_datasource_Root(global::System.Object sender, global::System.ComponentModel.PropertyChangedEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithBoolValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithBoolValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithIntValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithIntValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithFloatValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithFloatValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithUIntValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithUIntValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithColorValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithColorValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithDoubleValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithDoubleValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithVec2ValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithVec2ValueEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void HandleViewModelPropertyChangeOf_datasource_Root(global::System.String propertyName)
		{
			//DataSource property section
			//Primitive property section
		}
		
		private void RefreshDataSource_datasource_Root(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM newDataSource)
		{
			//Clear Section
			if (_datasource_Root != null)
			{
				{
					//Requires component data to be cleared
					var widgetComponent = _widget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				//Binding path: Root
				_datasource_Root.PropertyChanged -= ViewModelPropertyChangedListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithValue -=ViewModelPropertyChangedWithValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithBoolValue -=ViewModelPropertyChangedWithBoolValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithIntValue -=ViewModelPropertyChangedWithIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithFloatValue -=ViewModelPropertyChangedWithFloatValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithUIntValue -=ViewModelPropertyChangedWithUIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithColorValue -=ViewModelPropertyChangedWithColorValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithDoubleValue -=ViewModelPropertyChangedWithDoubleValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithVec2Value -=ViewModelPropertyChangedWithVec2ValueListenerOf_datasource_Root;
				_datasource_Root = null;
			}
			
			_datasource_Root = newDataSource; 
			
			//Assign Section
			if (_datasource_Root != null)
			{
				//Binding path: Root
				_datasource_Root.PropertyChanged += ViewModelPropertyChangedListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithValue +=ViewModelPropertyChangedWithValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithBoolValue +=ViewModelPropertyChangedWithBoolValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithIntValue +=ViewModelPropertyChangedWithIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithFloatValue +=ViewModelPropertyChangedWithFloatValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithUIntValue +=ViewModelPropertyChangedWithUIntValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithColorValue +=ViewModelPropertyChangedWithColorValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithDoubleValue +=ViewModelPropertyChangedWithDoubleValueListenerOf_datasource_Root;
				_datasource_Root.PropertyChangedWithVec2Value +=ViewModelPropertyChangedWithVec2ValueListenerOf_datasource_Root;
				{
					//Requires component data assignment
					var widgetComponent = _widget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				{
					//Requires component data assignment
					var widgetComponent = _widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
			}
		}
		
	}
	
}

