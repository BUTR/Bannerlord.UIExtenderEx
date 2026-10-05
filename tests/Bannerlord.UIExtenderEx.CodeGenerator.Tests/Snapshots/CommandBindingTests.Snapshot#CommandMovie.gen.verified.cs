namespace Bannerlord.UIExtenderEx.Tests.Generated
{
	//Data: DataSourceType - Bannerlord.UIExtenderEx.Tests.CodeGenerator.CommandVM
	public class CommandMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_CommandVM :  global::TaleWorlds.GauntletUI.BaseTypes.Widget,  global::TaleWorlds.GauntletUI.Data.IGeneratedGauntletMovieRoot
	{
		private global::TaleWorlds.GauntletUI.BaseTypes.Widget _widget;
		private global::TaleWorlds.GauntletUI.BaseTypes.ButtonWidget _widget_0;
		private global::TaleWorlds.GauntletUI.BaseTypes.ButtonWidget _widget_1;
		private CommandMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_CommandVM_Dependency_1_CommandPart__DependendPrefab _widget_2;
		private CommandMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_CommandVM_Dependency_2_CommandPart__DependendPrefab _widget_3;
		private global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.CommandVM _datasource_Root;
		
		public CommandMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_CommandVM(global::TaleWorlds.GauntletUI.UIContext context) : base(context)
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
			_widget_0 = new global::TaleWorlds.GauntletUI.BaseTypes.ButtonWidget(this.Context);
			//Requires data component
			{
				if (_widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget_0);
					_widget_0.AddComponent(widgetComponent);
				}
			}
			_widget.AddChild(_widget_0);
			_widget_1 = new global::TaleWorlds.GauntletUI.BaseTypes.ButtonWidget(this.Context);
			//Requires data component
			{
				if (_widget_1.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget_1);
					_widget_1.AddComponent(widgetComponent);
				}
			}
			_widget.AddChild(_widget_1);
			_widget_2 = new CommandMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_CommandVM_Dependency_1_CommandPart__DependendPrefab(this.Context);
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
			_widget_3 = new CommandMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_CommandVM_Dependency_2_CommandPart__DependendPrefab(this.Context);
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
		}
		
		public void SetIds()
		{
			this.Id = "";
			_widget_0.Id = "Plain";
			_widget_1.Id = "Literal";
			_widget_2.SetIds();
			_widget_2.Id = "";
			_widget_3.SetIds();
			_widget_3.Id = "";
		}
		
		public void SetAttributes()
		{
			
			//
			//TaleWorlds.GauntletUI.Data.WidgetAttributeKeyTypeCommand
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeDefault
			//Click ExecuteNoArgs
			//
			
			//
			//TaleWorlds.GauntletUI.Data.WidgetAttributeKeyTypeCommand
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeDefault
			//Click ExecuteWithTag
			//
			//
			//TaleWorlds.GauntletUI.Data.WidgetAttributeKeyTypeCommandParameter
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeDefault
			//Click literal
			//
			
			_widget_2.SetAttributes();
			//
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeKeyTypeParameter
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeDefault
			//Tag passed
			//
			
			_widget_3.SetAttributes();
			
		}
		
		public void RefreshBindingWithChildren()
		{
			var dataSource = _datasource_Root;
			this.SetDataSource(null);
			this.SetDataSource(dataSource);
		}
		
		public void DestroyDataSource()
		{
			_widget_0.EventFire -= EventListenerOf_widget_0;
			_widget_1.EventFire -= EventListenerOf_widget_1;
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
		
		public void SetDataSource(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.CommandVM dataSource)
		{
			RefreshDataSource_datasource_Root(dataSource);
		}
		
		private void EventListenerOf_widget_0(global::TaleWorlds.GauntletUI.BaseTypes.Widget widget, global::System.String commandName, global::System.Object[] args)
		{
			if (commandName == "Click")
			{
				if (_datasource_Root != null)
				{
					try
					{
						_datasource_Root.ExecuteNoArgs();
					}
					catch (global::System.Exception uiExtenderExCommandFailure)
					{
						throw new global::System.Reflection.TargetInvocationException(uiExtenderExCommandFailure);
					}
				}
			}
		}
		
		private void EventListenerOf_widget_1(global::TaleWorlds.GauntletUI.BaseTypes.Widget widget, global::System.String commandName, global::System.Object[] args)
		{
			if (commandName == "Click")
			{
				var arguments = new global::System.Object[args.Length + 1];
				for (var i = 0; i < args.Length; i++)
				{
					var value = args[i];
					var valueWidget = value as global::TaleWorlds.GauntletUI.BaseTypes.Widget;
					if (valueWidget != null)
					{
						var valueWidgetData = valueWidget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
						value = valueWidgetData == null ? null : valueWidgetData.Data;
					}
					arguments[i] = value;
				}
				//GotParameter literal
				arguments[args.Length] = "literal";
				if (_datasource_Root != null)
				{
					if (arguments.Length == 1)
					{
						var commandArgument0 = arguments[0];
						if ((commandArgument0 == null || commandArgument0 is global::System.String))
						{
							try
							{
								_datasource_Root.ExecuteWithTag((global::System.String)commandArgument0);
							}
							catch (global::System.Exception uiExtenderExCommandFailure)
							{
								throw new global::System.Reflection.TargetInvocationException(uiExtenderExCommandFailure);
							}
						}
					}
				}
			}
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
		
		private void RefreshDataSource_datasource_Root(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.CommandVM newDataSource)
		{
			//Clear Section
			_widget_0.EventFire -= EventListenerOf_widget_0;
			_widget_1.EventFire -= EventListenerOf_widget_1;
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
				_widget_0.EventFire += EventListenerOf_widget_0;
				{
					//Requires component data assignment
					var widgetComponent = _widget_1.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				_widget_1.EventFire += EventListenerOf_widget_1;
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
				_widget_2.SetDataSource(_datasource_Root);
				_widget_3.SetDataSource(_datasource_Root);
			}
			if (_datasource_Root == null)
			{
				_widget_0.EventFire += EventListenerOf_widget_0;
				_widget_1.EventFire += EventListenerOf_widget_1;
				_widget_2.SetDataSource(null);
				_widget_3.SetDataSource(null);
			}
		}
		
	}
	
	//Data: DataSourceType - Bannerlord.UIExtenderEx.Tests.CodeGenerator.CommandVM
	//Given Parameter: Tag - passed TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeKeyTypeParameter TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeDefault
	public class CommandMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_CommandVM_Dependency_1_CommandPart__DependendPrefab :  global::TaleWorlds.GauntletUI.BaseTypes.ButtonWidget
	{
		private global::TaleWorlds.GauntletUI.BaseTypes.ButtonWidget _widget;
		private global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.CommandVM _datasource_Root;
		
		public CommandMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_CommandVM_Dependency_1_CommandPart__DependendPrefab(global::TaleWorlds.GauntletUI.UIContext context) : base(context)
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
			//
			//TaleWorlds.GauntletUI.Data.WidgetAttributeKeyTypeCommand
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeDefault
			//Click ExecuteWithTag
			//
			//
			//TaleWorlds.GauntletUI.Data.WidgetAttributeKeyTypeCommandParameter
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeParameter
			//Click Tag
			//
			
		}
		
		public void DestroyDataSource()
		{
			_widget.EventFire -= EventListenerOf_widget;
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
		
		public void SetDataSource(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.CommandVM dataSource)
		{
			RefreshDataSource_datasource_Root(dataSource);
		}
		
		private void EventListenerOf_widget(global::TaleWorlds.GauntletUI.BaseTypes.Widget widget, global::System.String commandName, global::System.Object[] args)
		{
			if (commandName == "Click")
			{
				var arguments = new global::System.Object[args.Length + 1];
				for (var i = 0; i < args.Length; i++)
				{
					var value = args[i];
					var valueWidget = value as global::TaleWorlds.GauntletUI.BaseTypes.Widget;
					if (valueWidget != null)
					{
						var valueWidgetData = valueWidget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
						value = valueWidgetData == null ? null : valueWidgetData.Data;
					}
					arguments[i] = value;
				}
				//GotParameter passed
				arguments[args.Length] = "passed";
				if (_datasource_Root != null)
				{
					if (arguments.Length == 1)
					{
						var commandArgument0 = arguments[0];
						if ((commandArgument0 == null || commandArgument0 is global::System.String))
						{
							try
							{
								_datasource_Root.ExecuteWithTag((global::System.String)commandArgument0);
							}
							catch (global::System.Exception uiExtenderExCommandFailure)
							{
								throw new global::System.Reflection.TargetInvocationException(uiExtenderExCommandFailure);
							}
						}
					}
				}
			}
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
		
		private void RefreshDataSource_datasource_Root(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.CommandVM newDataSource)
		{
			//Clear Section
			_widget.EventFire -= EventListenerOf_widget;
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
				_widget.EventFire += EventListenerOf_widget;
			}
			if (_datasource_Root == null)
			{
				_widget.EventFire += EventListenerOf_widget;
			}
		}
		
	}
	
	//Data: DataSourceType - Bannerlord.UIExtenderEx.Tests.CodeGenerator.CommandVM
	public class CommandMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_CommandVM_Dependency_2_CommandPart__DependendPrefab :  global::TaleWorlds.GauntletUI.BaseTypes.ButtonWidget
	{
		private global::TaleWorlds.GauntletUI.BaseTypes.ButtonWidget _widget;
		private global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.CommandVM _datasource_Root;
		
		public CommandMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_CommandVM_Dependency_2_CommandPart__DependendPrefab(global::TaleWorlds.GauntletUI.UIContext context) : base(context)
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
			//
			//TaleWorlds.GauntletUI.Data.WidgetAttributeKeyTypeCommand
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeDefault
			//Click ExecuteWithTag
			//
			//
			//TaleWorlds.GauntletUI.Data.WidgetAttributeKeyTypeCommandParameter
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeParameter
			//Click Tag
			//
			
		}
		
		public void DestroyDataSource()
		{
			_widget.EventFire -= EventListenerOf_widget;
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
		
		public void SetDataSource(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.CommandVM dataSource)
		{
			RefreshDataSource_datasource_Root(dataSource);
		}
		
		private void EventListenerOf_widget(global::TaleWorlds.GauntletUI.BaseTypes.Widget widget, global::System.String commandName, global::System.Object[] args)
		{
			if (commandName == "Click")
			{
				var arguments = new global::System.Object[args.Length];
				for (var i = 0; i < args.Length; i++)
				{
					var value = args[i];
					var valueWidget = value as global::TaleWorlds.GauntletUI.BaseTypes.Widget;
					if (valueWidget != null)
					{
						var valueWidgetData = valueWidget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
						value = valueWidgetData == null ? null : valueWidgetData.Data;
					}
					arguments[i] = value;
				}
				if (_datasource_Root != null)
				{
					if (arguments.Length == 1)
					{
						var commandArgument0 = arguments[0];
						if ((commandArgument0 == null || commandArgument0 is global::System.String))
						{
							try
							{
								_datasource_Root.ExecuteWithTag((global::System.String)commandArgument0);
							}
							catch (global::System.Exception uiExtenderExCommandFailure)
							{
								throw new global::System.Reflection.TargetInvocationException(uiExtenderExCommandFailure);
							}
						}
					}
				}
			}
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
		
		private void RefreshDataSource_datasource_Root(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.CommandVM newDataSource)
		{
			//Clear Section
			_widget.EventFire -= EventListenerOf_widget;
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
				_widget.EventFire += EventListenerOf_widget;
			}
			if (_datasource_Root == null)
			{
				_widget.EventFire += EventListenerOf_widget;
			}
		}
		
	}
	
}

