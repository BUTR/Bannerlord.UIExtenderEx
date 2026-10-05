namespace Bannerlord.UIExtenderEx.Tests.Generated
{
	//Data: DataSourceType - Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM
	public class StructureMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM :  global::TaleWorlds.GauntletUI.BaseTypes.Widget,  global::TaleWorlds.GauntletUI.Data.IGeneratedGauntletMovieRoot
	{
		private global::TaleWorlds.GauntletUI.BaseTypes.Widget _widget;
		private global::TaleWorlds.GauntletUI.BaseTypes.Widget _widget_0;
		private global::TaleWorlds.GauntletUI.BaseTypes.ScrollablePanel _widget_1;
		private global::TaleWorlds.GauntletUI.BaseTypes.Widget _widget_1_0;
		private StructureMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_1_StructurePart__DependendPrefab _widget_2;
		private StructureMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_1_StructurePart__DependendPrefab _widget_3;
		private StructureMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_2_StructurePart__DependendPrefab _widget_4;
		private global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM _datasource_Root;
		
		public StructureMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM(global::TaleWorlds.GauntletUI.UIContext context) : base(context)
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
			_widget_0 = new global::TaleWorlds.GauntletUI.BaseTypes.Widget(this.Context);
			//Requires data component
			{
				if (_widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget_0);
					_widget_0.AddComponent(widgetComponent);
				}
			}
			_widget.AddChild(_widget_0);
			_widget_1 = new global::TaleWorlds.GauntletUI.BaseTypes.ScrollablePanel(this.Context);
			//Requires data component
			{
				if (_widget_1.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget_1);
					_widget_1.AddComponent(widgetComponent);
				}
			}
			_widget.AddChild(_widget_1);
			_widget_1_0 = new global::TaleWorlds.GauntletUI.BaseTypes.Widget(this.Context);
			//Requires data component
			{
				if (_widget_1_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget_1_0);
					_widget_1_0.AddComponent(widgetComponent);
				}
			}
			_widget_1.AddChild(_widget_1_0);
			_widget_2 = new StructureMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_1_StructurePart__DependendPrefab(this.Context);
			//Requires data component
			{
				if (_widget_2.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget_2);
					_widget_2.AddComponent(widgetComponent);
				}
			}
			_widget.AddChild(_widget_2);
			_widget_2.CreateWidgets();
			_widget_3 = new StructureMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_1_StructurePart__DependendPrefab(this.Context);
			//Requires data component
			{
				if (_widget_3.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget_3);
					_widget_3.AddComponent(widgetComponent);
				}
			}
			_widget.AddChild(_widget_3);
			_widget_3.CreateWidgets();
			_widget_4 = new StructureMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_2_StructurePart__DependendPrefab(this.Context);
			//Requires data component
			{
				if (_widget_4.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget_4);
					_widget_4.AddComponent(widgetComponent);
				}
			}
			_widget.AddChild(_widget_4);
			_widget_4.CreateWidgets();
		}
		
		public void SetIds()
		{
			this.Id = "RootWidget";
			_widget_0.Id = "Sibling";
			_widget_1.Id = "Panel";
			_widget_1_0.Id = "Inner";
			_widget_2.SetIds();
			_widget_2.Id = "";
			_widget_3.SetIds();
			_widget_3.Id = "";
			_widget_4.SetIds();
			_widget_4.Id = "";
		}
		
		public void SetAttributes()
		{
			
			
			//Widget found by path when the attributes are set - Inner
			global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttributeFromString(_widget_1, @"InnerPanel", @"Inner", _widget_1.Context.BrushFactory, _widget_1.Context.SpriteData, null, null, null, null, null);
			//Widget found by path when the attributes are set - ..\Sibling
			global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttributeFromString(_widget_1, @"FixedHeader", @"..\Sibling", _widget_1.Context.BrushFactory, _widget_1.Context.SpriteData, null, null, null, null, null);
			//Widget found by path when the attributes are set - NotThere
			global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttributeFromString(_widget_1, @"ClipRect", @"NotThere", _widget_1.Context.BrushFactory, _widget_1.Context.SpriteData, null, null, null, null, null);
			//Widget found by path when the attributes are set - Nowhere\Deeper
			global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttributeFromString(_widget_1, @"ScrolledHeader", @"Nowhere\Deeper", _widget_1.Context.BrushFactory, _widget_1.Context.SpriteData, null, null, null, null, null);
			
			
			_widget_2.SetAttributes();
			//
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeKeyTypeParameter
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeDefault
			//Label one
			//
			
			_widget_3.SetAttributes();
			//
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeKeyTypeParameter
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeDefault
			//Label one
			//
			
			_widget_4.SetAttributes();
			//
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeKeyTypeParameter
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeDefault
			//Label two
			//
			
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
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_1.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_1_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				_widget_2.DestroyDataSource();
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_2.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				_widget_3.DestroyDataSource();
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_3.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				_widget_4.DestroyDataSource();
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_4.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
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
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_1.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_1_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				_widget_2.SetDataSource(null);
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_2.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				_widget_3.SetDataSource(null);
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_3.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				_widget_4.SetDataSource(null);
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_4.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
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
				{
					//Requires component data assignment
					var widgetComponent = _widget_1.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				{
					//Requires component data assignment
					var widgetComponent = _widget_1_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				{
					//Requires component data assignment
					var widgetComponent = _widget_2.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				{
					//Requires component data assignment
					var widgetComponent = _widget_3.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				{
					//Requires component data assignment
					var widgetComponent = _widget_4.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				_widget_2.SetDataSource(_datasource_Root);
				_widget_3.SetDataSource(_datasource_Root);
				_widget_4.SetDataSource(_datasource_Root);
			}
			if (_datasource_Root == null)
			{
				_widget_2.SetDataSource(null);
				_widget_3.SetDataSource(null);
				_widget_4.SetDataSource(null);
			}
		}
		
	}
	
	//Data: DataSourceType - Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM
	//Given Parameter: Label - one TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeKeyTypeParameter TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeDefault
	public class StructureMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_1_StructurePart__DependendPrefab :  global::TaleWorlds.GauntletUI.BaseTypes.TextWidget
	{
		private global::TaleWorlds.GauntletUI.BaseTypes.TextWidget _widget;
		private global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM _datasource_Root;
		
		public StructureMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_1_StructurePart__DependendPrefab(global::TaleWorlds.GauntletUI.UIContext context) : base(context)
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
		}
		
		public void SetIds()
		{
			this.Id = "";
		}
		
		public void SetAttributes()
		{
			//From parameter Label:one
			try
			{
				this.Text = @"one";
			}
			catch (global::System.Exception)
			{
				global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.LoaderAsserts.SetterThrew(this, "Text", "one");
			}
			
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
	//Given Parameter: Label - two TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeKeyTypeParameter TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeDefault
	public class StructureMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_2_StructurePart__DependendPrefab :  global::TaleWorlds.GauntletUI.BaseTypes.TextWidget
	{
		private global::TaleWorlds.GauntletUI.BaseTypes.TextWidget _widget;
		private global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM _datasource_Root;
		
		public StructureMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM_Dependency_2_StructurePart__DependendPrefab(global::TaleWorlds.GauntletUI.UIContext context) : base(context)
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
		}
		
		public void SetIds()
		{
			this.Id = "";
		}
		
		public void SetAttributes()
		{
			//From parameter Label:two
			try
			{
				this.Text = @"two";
			}
			catch (global::System.Exception)
			{
				global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.LoaderAsserts.SetterThrew(this, "Text", "two");
			}
			
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
	
}

