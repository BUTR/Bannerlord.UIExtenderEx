namespace Bannerlord.UIExtenderEx.Tests.Generated
{
	//Data: DataSourceType - Bannerlord.UIExtenderEx.Tests.CodeGenerator.DerivedItemListVM
	public class DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM :  global::TaleWorlds.GauntletUI.BaseTypes.Widget,  global::TaleWorlds.GauntletUI.Data.IGeneratedGauntletMovieRoot
	{
		private global::TaleWorlds.GauntletUI.BaseTypes.Widget _widget;
		private global::TaleWorlds.GauntletUI.BaseTypes.ListPanel _widget_0;
		private global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.DerivedItemListVM _datasource_Root;
		private global::TaleWorlds.Library.MBBindingList<global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.DeclaredItemVM> _datasource_Root_ItemList;
		
		public DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM(global::TaleWorlds.GauntletUI.UIContext context) : base(context)
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
			_widget_0 = new global::TaleWorlds.GauntletUI.BaseTypes.ListPanel(this.Context);
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
		
		public void SetIds()
		{
			this.Id = "";
			_widget_0.Id = "Items";
		}
		
		public void SetAttributes()
		{
			
			//
			//TaleWorlds.GauntletUI.Data.WidgetAttributeKeyTypeDataSource
			//TaleWorlds.GauntletUI.Data.WidgetAttributeValueTypeBindingPath
			//DataSource ItemList
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
				if (_datasource_Root_ItemList != null)
				{
					{
						//Requires component data to be cleared
						var widgetComponent = _widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
						widgetComponent.Data = null;
					}
					_datasource_Root_ItemList.ListChanged -= OnList_datasource_Root_ItemListChanged;
					//Binding path list: Root\ItemList
					for (var i = _widget_0.ChildCount - 1; i >= 0; i--)
					{
						{
							var widget = _widget_0.GetChild(i);
							var targetWidget = (DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM_Dependency_1_ItemTemplate)widget;
							targetWidget.OnBeforeRemovedChild(widget);
						}
						{
							var widget = _widget_0.GetChild(i);
							var targetWidget = (DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM_Dependency_1_ItemTemplate)widget;
							targetWidget.DestroyDataSource();
						}
					}
					_datasource_Root_ItemList = null;
				}
				_datasource_Root = null;
			}
		}
		
		public void SetDataSource(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.DerivedItemListVM dataSource)
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
			if (propertyName == "ItemList")
			{
				RefreshDataSource_datasource_Root_ItemList(_datasource_Root.ItemList);
				return;
			}
			//Primitive property section
		}
		
		public void OnList_datasource_Root_ItemListChanged(global::System.Object sender, global::TaleWorlds.Library.ListChangedEventArgs e)
		{
			switch (e.ListChangedType)
			{
				case global::TaleWorlds.Library.ListChangedType.Reset:
				{
					for (var i = _widget_0.ChildCount - 1; i >= 0; i--)
					{
						{
							var widget = _widget_0.GetChild(i);
							var targetWidget = (DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM_Dependency_1_ItemTemplate)widget;
							targetWidget.OnBeforeRemovedChild(widget);
						}
						{
							var widget = _widget_0.GetChild(i);
							var targetWidget = (DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM_Dependency_1_ItemTemplate)widget;
							targetWidget.SetDataSource(null);
							_widget_0.RemoveChild(widget);
						}
					}
				}
				break;
				case global::TaleWorlds.Library.ListChangedType.Sorted:
				{
					for (int i = 0; i < _datasource_Root_ItemList.Count; i++)
					{
						var bindingObject = _datasource_Root_ItemList[i];
						{
							var target = _widget_0.FindChild(widget => widget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>().Data == bindingObject);
							target.SetSiblingIndex(i);
						}
					}
				}
				break;
				case global::TaleWorlds.Library.ListChangedType.ItemAdded:
				{
					{
						var itemsBefore = _datasource_Root_ItemList.Count - 1;
						{
							var item = new DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM_Dependency_1_ItemTemplate(this.Context);
							var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(item);
							var dataSource = _datasource_Root_ItemList[e.NewIndex];
							widgetComponent.Data = dataSource;
							item.AddComponent(widgetComponent);
							_widget_0.AddChild(item);
							item.CreateWidgets();
							item.SetIds();
							item.SetAttributes();
							item.SetSiblingIndex(e.NewIndex);
							item.SetDataSource(dataSource);
						}
					}
				}
				break;
				case global::TaleWorlds.Library.ListChangedType.ItemBeforeDeleted:
				{
					{
						{
							var widget = _widget_0.GetChild(e.NewIndex);
							var targetWidget = (DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM_Dependency_1_ItemTemplate)widget;
							targetWidget.OnBeforeRemovedChild(widget);
						}
					}
				}
				break;
				case global::TaleWorlds.Library.ListChangedType.ItemDeleted:
				{
					{
						{
							var widget = _widget_0.GetChild(e.NewIndex);
							var targetWidget = (DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM_Dependency_1_ItemTemplate)widget;
							targetWidget.SetDataSource(null);
							_widget_0.RemoveChild(widget);
						}
					}
				}
				break;
				case global::TaleWorlds.Library.ListChangedType.ItemChanged:
				{
					
				}
				break;
			}
		}
		
		private void RefreshDataSource_datasource_Root(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.DerivedItemListVM newDataSource)
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
				if (_datasource_Root_ItemList != null)
				{
					{
						//Requires component data to be cleared
						var widgetComponent = _widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
						widgetComponent.Data = null;
					}
					_datasource_Root_ItemList.ListChanged -= OnList_datasource_Root_ItemListChanged;
					//Binding path list: Root\ItemList
					for (var i = _widget_0.ChildCount - 1; i >= 0; i--)
					{
						{
							var widget = _widget_0.GetChild(i);
							var targetWidget = (DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM_Dependency_1_ItemTemplate)widget;
							targetWidget.OnBeforeRemovedChild(widget);
						}
						{
							var widget = _widget_0.GetChild(i);
							var targetWidget = (DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM_Dependency_1_ItemTemplate)widget;
							targetWidget.SetDataSource(null);
							_widget_0.RemoveChild(widget);
						}
					}
					_datasource_Root_ItemList = null;
				}
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
				_datasource_Root_ItemList = _datasource_Root.ItemList;
				if (_datasource_Root_ItemList != null)
				{
					_datasource_Root_ItemList.ListChanged += OnList_datasource_Root_ItemListChanged;
					//Binding path list: Root\ItemList
					for (var i = 0; i < _datasource_Root_ItemList.Count; i++)
					{
						{
							var itemsBefore = i;
							{
								var item = new DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM_Dependency_1_ItemTemplate(this.Context);
								var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(item);
								var dataSource = _datasource_Root_ItemList[i];
								widgetComponent.Data = dataSource;
								item.AddComponent(widgetComponent);
								_widget_0.AddChild(item);
								item.CreateWidgets();
								item.SetIds();
								item.SetAttributes();
								item.SetSiblingIndex(i);
								item.SetDataSource(dataSource);
							}
						}
					}
					{
						//Requires component data assignment
						var widgetComponent = _widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
						widgetComponent.Data = _datasource_Root_ItemList;
					}
				}
			}
		}
		
		private void RefreshDataSource_datasource_Root_ItemList(global::TaleWorlds.Library.MBBindingList<global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.DeclaredItemVM> newDataSource)
		{
			//Clear Section
			if (_datasource_Root_ItemList != null)
			{
				{
					//Requires component data to be cleared
					var widgetComponent = _widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
				_datasource_Root_ItemList.ListChanged -= OnList_datasource_Root_ItemListChanged;
				//Binding path list: Root\ItemList
				for (var i = _widget_0.ChildCount - 1; i >= 0; i--)
				{
					{
						var widget = _widget_0.GetChild(i);
						var targetWidget = (DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM_Dependency_1_ItemTemplate)widget;
						targetWidget.OnBeforeRemovedChild(widget);
					}
					{
						var widget = _widget_0.GetChild(i);
						var targetWidget = (DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM_Dependency_1_ItemTemplate)widget;
						targetWidget.SetDataSource(null);
						_widget_0.RemoveChild(widget);
					}
				}
				_datasource_Root_ItemList = null;
			}
			
			_datasource_Root_ItemList = newDataSource; 
			
			//Assign Section
			_datasource_Root_ItemList = _datasource_Root.ItemList;
			if (_datasource_Root_ItemList != null)
			{
				_datasource_Root_ItemList.ListChanged += OnList_datasource_Root_ItemListChanged;
				//Binding path list: Root\ItemList
				for (var i = 0; i < _datasource_Root_ItemList.Count; i++)
				{
					{
						var itemsBefore = i;
						{
							var item = new DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM_Dependency_1_ItemTemplate(this.Context);
							var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(item);
							var dataSource = _datasource_Root_ItemList[i];
							widgetComponent.Data = dataSource;
							item.AddComponent(widgetComponent);
							_widget_0.AddChild(item);
							item.CreateWidgets();
							item.SetIds();
							item.SetAttributes();
							item.SetSiblingIndex(i);
							item.SetDataSource(dataSource);
						}
					}
				}
				{
					//Requires component data assignment
					var widgetComponent = _widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root_ItemList;
				}
			}
		}
		
	}
	
	//Data: DataSourceType - Bannerlord.UIExtenderEx.Tests.CodeGenerator.DeclaredItemVM
	public class DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM_Dependency_1_ItemTemplate :  global::TaleWorlds.GauntletUI.BaseTypes.Widget
	{
		private global::TaleWorlds.GauntletUI.BaseTypes.Widget _widget;
		private global::TaleWorlds.GauntletUI.BaseTypes.TextWidget _widget_0;
		private global::TaleWorlds.GauntletUI.BaseTypes.Widget _widget_1;
		private global::TaleWorlds.GauntletUI.BaseTypes.TextWidget _widget_2;
		private global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.DeclaredItemVM _datasource_Root;
		
		public DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM_Dependency_1_ItemTemplate(global::TaleWorlds.GauntletUI.UIContext context) : base(context)
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
			_widget_1 = new global::TaleWorlds.GauntletUI.BaseTypes.Widget(this.Context);
			//Requires data component
			{
				if (_widget_1.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget_1);
					_widget_1.AddComponent(widgetComponent);
				}
			}
			_widget.AddChild(_widget_1);
			_widget_2 = new global::TaleWorlds.GauntletUI.BaseTypes.TextWidget(this.Context);
			//Requires data component
			{
				if (_widget_2.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget_2);
					_widget_2.AddComponent(widgetComponent);
				}
			}
			_widget.AddChild(_widget_2);
		}
		
		public void SetIds()
		{
			this.Id = "";
			_widget_0.Id = "BaseText";
			_widget_1.Id = "Indicator";
			_widget_2.Id = "IndicatorText";
		}
		
		public void SetAttributes()
		{
			
			//
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeKeyTypeAttribute
			//TaleWorlds.GauntletUI.Data.WidgetAttributeValueTypeBinding
			//Text BaseText
			//
			
			//
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeKeyTypeAttribute
			//TaleWorlds.GauntletUI.Data.WidgetAttributeValueTypeBinding
			//IsVisible HasIndicator
			//
			
			//
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeKeyTypeAttribute
			//TaleWorlds.GauntletUI.Data.WidgetAttributeValueTypeBinding
			//IntText IndicatorCount
			//
			
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
					var widgetComponent = _widget_2.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
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
				_widget_0.PropertyChanged -= PropertyChangedListenerOf_widget_0;
				_widget_1.boolPropertyChanged -= boolPropertyChangedListenerOf_widget_1;
				_widget_2.intPropertyChanged -= intPropertyChangedListenerOf_widget_2;
				_datasource_Root = null;
			}
		}
		
		public void SetDataSource(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.DeclaredItemVM dataSource)
		{
			RefreshDataSource_datasource_Root(dataSource);
		}
		
		private void PropertyChangedListenerOf_widget_0(global::TaleWorlds.GauntletUI.PropertyOwnerObject propertyOwnerObject, global::System.String propertyName, global::System.Object e)
		{
			HandleWidgetPropertyChangeOf_widget_0(propertyName, e);
		}
		
		private void HandleWidgetPropertyChangeOf_widget_0(global::System.String propertyName, global::System.Object value)
		{
			if (propertyName == "Text")
			{
				//Property in ViewModel does not have a set method
				//_datasource_Root.BaseText = _widget_0.Text;
				return;
			}
		}
		
		private void boolPropertyChangedListenerOf_widget_1(global::TaleWorlds.GauntletUI.PropertyOwnerObject propertyOwnerObject, global::System.String propertyName, bool e)
		{
			HandleWidgetPropertyChangeOf_widget_1(propertyName, e);
		}
		
		private void HandleWidgetPropertyChangeOf_widget_1(global::System.String propertyName, global::System.Object value)
		{
			if (propertyName == "IsVisible")
			{
				//Not declared on the ViewModel type; bound by name, as the XML loader does
				global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.DynamicMember.Set(_datasource_Root, "HasIndicator", value);
				return;
			}
		}
		
		private void intPropertyChangedListenerOf_widget_2(global::TaleWorlds.GauntletUI.PropertyOwnerObject propertyOwnerObject, global::System.String propertyName, int e)
		{
			HandleWidgetPropertyChangeOf_widget_2(propertyName, e);
		}
		
		private void HandleWidgetPropertyChangeOf_widget_2(global::System.String propertyName, global::System.Object value)
		{
			if (propertyName == "IntText")
			{
				//Not declared on the ViewModel type; bound by name, as the XML loader does
				global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.DynamicMember.Set(_datasource_Root, "IndicatorCount", value);
				return;
			}
		}
		
		private void ViewModelPropertyChangedListenerOf_datasource_Root(global::System.Object sender, global::System.ComponentModel.PropertyChangedEventArgs e)
		{
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private global::System.Boolean HandleViewModelPropertyChangeWithValueOf_datasource_Root(global::System.String propertyName, global::System.Object value)
		{
			//A child data source changing is not a widget value; the rereading handler deals with it
			if (value is global::TaleWorlds.Library.ViewModel || value is global::TaleWorlds.Library.IMBBindingList)
			{
				return false;
			}
			if (propertyName == "BaseText")
			{
				if (value is global::System.String uiExtenderExAssigned__widget_0_Text)
				{
					try
					{
						_widget_0.Text = uiExtenderExAssigned__widget_0_Text;
					}
					catch (global::System.Exception uiExtenderExSetterFailure__widget_0_Text)
					{
						throw new global::System.Reflection.TargetInvocationException(uiExtenderExSetterFailure__widget_0_Text);
					}
				}
				else
				{
					global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_0, "Text", value);
				}
				return true;
			}
			if (propertyName == "HasIndicator")
			{
				if (value is global::System.Boolean uiExtenderExAssigned__widget_1_IsVisible)
				{
					try
					{
						_widget_1.IsVisible = uiExtenderExAssigned__widget_1_IsVisible;
					}
					catch (global::System.Exception uiExtenderExSetterFailure__widget_1_IsVisible)
					{
						throw new global::System.Reflection.TargetInvocationException(uiExtenderExSetterFailure__widget_1_IsVisible);
					}
				}
				else
				{
					global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "IsVisible", value);
				}
				return true;
			}
			if (propertyName == "IndicatorCount")
			{
				if (value is global::System.Int32 uiExtenderExAssigned__widget_2_IntText)
				{
					try
					{
						_widget_2.IntText = uiExtenderExAssigned__widget_2_IntText;
					}
					catch (global::System.Exception uiExtenderExSetterFailure__widget_2_IntText)
					{
						throw new global::System.Reflection.TargetInvocationException(uiExtenderExSetterFailure__widget_2_IntText);
					}
				}
				else
				{
					global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_2, "IntText", value);
				}
				return true;
			}
			return false;
		}
		
		private global::System.Boolean HandleViewModelPropertyChangeWithBoolValueOf_datasource_Root(global::System.String propertyName, global::System.Boolean value)
		{
			if (propertyName == "BaseText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_0, "Text", value);
				return true;
			}
			if (propertyName == "HasIndicator")
			{
				try
				{
					_widget_1.IsVisible = value;
				}
				catch (global::System.Exception uiExtenderExSetterFailure__widget_1_IsVisible)
				{
					throw new global::System.Reflection.TargetInvocationException(uiExtenderExSetterFailure__widget_1_IsVisible);
				}
				return true;
			}
			if (propertyName == "IndicatorCount")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_2, "IntText", value);
				return true;
			}
			return false;
		}
		
		private global::System.Boolean HandleViewModelPropertyChangeWithIntValueOf_datasource_Root(global::System.String propertyName, global::System.Int32 value)
		{
			if (propertyName == "BaseText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_0, "Text", value);
				return true;
			}
			if (propertyName == "HasIndicator")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "IsVisible", value);
				return true;
			}
			if (propertyName == "IndicatorCount")
			{
				try
				{
					_widget_2.IntText = value;
				}
				catch (global::System.Exception uiExtenderExSetterFailure__widget_2_IntText)
				{
					throw new global::System.Reflection.TargetInvocationException(uiExtenderExSetterFailure__widget_2_IntText);
				}
				return true;
			}
			return false;
		}
		
		private global::System.Boolean HandleViewModelPropertyChangeWithFloatValueOf_datasource_Root(global::System.String propertyName, global::System.Single value)
		{
			if (propertyName == "BaseText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_0, "Text", value);
				return true;
			}
			if (propertyName == "HasIndicator")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "IsVisible", value);
				return true;
			}
			if (propertyName == "IndicatorCount")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_2, "IntText", value);
				return true;
			}
			return false;
		}
		
		private global::System.Boolean HandleViewModelPropertyChangeWithUIntValueOf_datasource_Root(global::System.String propertyName, global::System.UInt32 value)
		{
			if (propertyName == "BaseText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_0, "Text", value);
				return true;
			}
			if (propertyName == "HasIndicator")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "IsVisible", value);
				return true;
			}
			if (propertyName == "IndicatorCount")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_2, "IntText", value);
				return true;
			}
			return false;
		}
		
		private global::System.Boolean HandleViewModelPropertyChangeWithColorValueOf_datasource_Root(global::System.String propertyName, global::TaleWorlds.Library.Color value)
		{
			if (propertyName == "BaseText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_0, "Text", value);
				return true;
			}
			if (propertyName == "HasIndicator")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "IsVisible", value);
				return true;
			}
			if (propertyName == "IndicatorCount")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_2, "IntText", value);
				return true;
			}
			return false;
		}
		
		private global::System.Boolean HandleViewModelPropertyChangeWithDoubleValueOf_datasource_Root(global::System.String propertyName, global::System.Double value)
		{
			if (propertyName == "BaseText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_0, "Text", value);
				return true;
			}
			if (propertyName == "HasIndicator")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "IsVisible", value);
				return true;
			}
			if (propertyName == "IndicatorCount")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_2, "IntText", value);
				return true;
			}
			return false;
		}
		
		private global::System.Boolean HandleViewModelPropertyChangeWithVec2ValueOf_datasource_Root(global::System.String propertyName, global::TaleWorlds.Library.Vec2 value)
		{
			if (propertyName == "BaseText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_0, "Text", value);
				return true;
			}
			if (propertyName == "HasIndicator")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "IsVisible", value);
				return true;
			}
			if (propertyName == "IndicatorCount")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_2, "IntText", value);
				return true;
			}
			return false;
		}
		
		private void ViewModelPropertyChangedWithValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithValueEventArgs e)
		{
			if (HandleViewModelPropertyChangeWithValueOf_datasource_Root(e.PropertyName, e.Value))
			{
				return;
			}
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithBoolValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithBoolValueEventArgs e)
		{
			if (HandleViewModelPropertyChangeWithBoolValueOf_datasource_Root(e.PropertyName, e.Value))
			{
				return;
			}
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithIntValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithIntValueEventArgs e)
		{
			if (HandleViewModelPropertyChangeWithIntValueOf_datasource_Root(e.PropertyName, e.Value))
			{
				return;
			}
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithFloatValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithFloatValueEventArgs e)
		{
			if (HandleViewModelPropertyChangeWithFloatValueOf_datasource_Root(e.PropertyName, e.Value))
			{
				return;
			}
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithUIntValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithUIntValueEventArgs e)
		{
			if (HandleViewModelPropertyChangeWithUIntValueOf_datasource_Root(e.PropertyName, e.Value))
			{
				return;
			}
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithColorValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithColorValueEventArgs e)
		{
			if (HandleViewModelPropertyChangeWithColorValueOf_datasource_Root(e.PropertyName, e.Value))
			{
				return;
			}
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithDoubleValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithDoubleValueEventArgs e)
		{
			if (HandleViewModelPropertyChangeWithDoubleValueOf_datasource_Root(e.PropertyName, e.Value))
			{
				return;
			}
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void ViewModelPropertyChangedWithVec2ValueListenerOf_datasource_Root(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWithVec2ValueEventArgs e)
		{
			if (HandleViewModelPropertyChangeWithVec2ValueOf_datasource_Root(e.PropertyName, e.Value))
			{
				return;
			}
			HandleViewModelPropertyChangeOf_datasource_Root(e.PropertyName);
		}
		
		private void HandleViewModelPropertyChangeOf_datasource_Root(global::System.String propertyName)
		{
			//DataSource property section
			//Primitive property section
			if (propertyName == "BaseText")
			{
				_widget_0.Text = _datasource_Root.BaseText;
				return;
			}
			if (propertyName == "HasIndicator")
			{
				//Not declared on the ViewModel type; bound by name, as the XML loader does
				{
					object uiExtenderExValue__widget_1_IsVisible = global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.DynamicMember.Get(_datasource_Root, "HasIndicator");
					if (uiExtenderExValue__widget_1_IsVisible is global::System.Boolean uiExtenderExAssigned__widget_1_IsVisible)
					{
						try
						{
							_widget_1.IsVisible = uiExtenderExAssigned__widget_1_IsVisible;
						}
						catch (global::System.Exception uiExtenderExSetterFailure__widget_1_IsVisible)
						{
							throw new global::System.Reflection.TargetInvocationException(uiExtenderExSetterFailure__widget_1_IsVisible);
						}
					}
					else
					{
						global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "IsVisible", uiExtenderExValue__widget_1_IsVisible);
					}
				}
				return;
			}
			if (propertyName == "IndicatorCount")
			{
				//Not declared on the ViewModel type; bound by name, as the XML loader does
				{
					object uiExtenderExValue__widget_2_IntText = global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.DynamicMember.Get(_datasource_Root, "IndicatorCount");
					if (uiExtenderExValue__widget_2_IntText is global::System.Int32 uiExtenderExAssigned__widget_2_IntText)
					{
						try
						{
							_widget_2.IntText = uiExtenderExAssigned__widget_2_IntText;
						}
						catch (global::System.Exception uiExtenderExSetterFailure__widget_2_IntText)
						{
							throw new global::System.Reflection.TargetInvocationException(uiExtenderExSetterFailure__widget_2_IntText);
						}
					}
					else
					{
						global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_2, "IntText", uiExtenderExValue__widget_2_IntText);
					}
				}
				return;
			}
		}
		
		private void RefreshDataSource_datasource_Root(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.DeclaredItemVM newDataSource)
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
					var widgetComponent = _widget_2.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
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
				_widget_0.PropertyChanged -= PropertyChangedListenerOf_widget_0;
				_widget_1.boolPropertyChanged -= boolPropertyChangedListenerOf_widget_1;
				_widget_2.intPropertyChanged -= intPropertyChangedListenerOf_widget_2;
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
				_widget_0.Text = _datasource_Root.BaseText;
				{
					//Requires component data assignment
					var widgetComponent = _widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				_widget_0.PropertyChanged += PropertyChangedListenerOf_widget_0;
				//Not declared on the ViewModel type; bound by name, as the XML loader does
				{
					object uiExtenderExValue__widget_1_IsVisible = global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.DynamicMember.Get(_datasource_Root, "HasIndicator");
					if (uiExtenderExValue__widget_1_IsVisible is global::System.Boolean uiExtenderExAssigned__widget_1_IsVisible)
					{
						try
						{
							_widget_1.IsVisible = uiExtenderExAssigned__widget_1_IsVisible;
						}
						catch (global::System.Exception uiExtenderExSetterFailure__widget_1_IsVisible)
						{
							throw new global::System.Reflection.TargetInvocationException(uiExtenderExSetterFailure__widget_1_IsVisible);
						}
					}
					else
					{
						global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "IsVisible", uiExtenderExValue__widget_1_IsVisible);
					}
				}
				{
					//Requires component data assignment
					var widgetComponent = _widget_1.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				_widget_1.boolPropertyChanged += boolPropertyChangedListenerOf_widget_1;
				//Not declared on the ViewModel type; bound by name, as the XML loader does
				{
					object uiExtenderExValue__widget_2_IntText = global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.DynamicMember.Get(_datasource_Root, "IndicatorCount");
					if (uiExtenderExValue__widget_2_IntText is global::System.Int32 uiExtenderExAssigned__widget_2_IntText)
					{
						try
						{
							_widget_2.IntText = uiExtenderExAssigned__widget_2_IntText;
						}
						catch (global::System.Exception uiExtenderExSetterFailure__widget_2_IntText)
						{
							throw new global::System.Reflection.TargetInvocationException(uiExtenderExSetterFailure__widget_2_IntText);
						}
					}
					else
					{
						global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_2, "IntText", uiExtenderExValue__widget_2_IntText);
					}
				}
				{
					//Requires component data assignment
					var widgetComponent = _widget_2.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				_widget_2.intPropertyChanged += intPropertyChangedListenerOf_widget_2;
			}
		}
		
	}
	
}

