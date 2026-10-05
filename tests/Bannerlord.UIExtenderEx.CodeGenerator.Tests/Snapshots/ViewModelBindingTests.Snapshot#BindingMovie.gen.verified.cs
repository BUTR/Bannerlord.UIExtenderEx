namespace Bannerlord.UIExtenderEx.Tests.Generated
{
	//Data: DataSourceType - Bannerlord.UIExtenderEx.Tests.CodeGenerator.NonPublicMemberVM
	public class BindingMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_NonPublicMemberVM :  global::TaleWorlds.GauntletUI.BaseTypes.Widget,  global::TaleWorlds.GauntletUI.Data.IGeneratedGauntletMovieRoot
	{
		private global::TaleWorlds.GauntletUI.BaseTypes.Widget _widget;
		private global::TaleWorlds.GauntletUI.BaseTypes.TextWidget _widget_0;
		private global::TaleWorlds.GauntletUI.BaseTypes.TextWidget _widget_1;
		private global::TaleWorlds.GauntletUI.BaseTypes.TextWidget _widget_2;
		private global::TaleWorlds.GauntletUI.BaseTypes.TextWidget _widget_3;
		private global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.NonPublicMemberVM _datasource_Root;
		
		public BindingMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_NonPublicMemberVM(global::TaleWorlds.GauntletUI.UIContext context) : base(context)
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
			_widget_1 = new global::TaleWorlds.GauntletUI.BaseTypes.TextWidget(this.Context);
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
			_widget_3 = new global::TaleWorlds.GauntletUI.BaseTypes.TextWidget(this.Context);
			//Requires data component
			{
				if (_widget_3.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>() == null)
				{
					var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData(_widget_3);
					_widget_3.AddComponent(widgetComponent);
				}
			}
			_widget.AddChild(_widget_3);
		}
		
		public void SetIds()
		{
			this.Id = "";
			_widget_0.Id = "PublicText";
			_widget_1.Id = "ReadOnlyText";
			_widget_2.Id = "InternalText";
			_widget_3.Id = "InternalSetterText";
		}
		
		public void SetAttributes()
		{
			
			//
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeKeyTypeAttribute
			//TaleWorlds.GauntletUI.Data.WidgetAttributeValueTypeBinding
			//Text PublicText
			//
			
			//
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeKeyTypeAttribute
			//TaleWorlds.GauntletUI.Data.WidgetAttributeValueTypeBinding
			//Text ReadOnlyText
			//
			
			//
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeKeyTypeAttribute
			//TaleWorlds.GauntletUI.Data.WidgetAttributeValueTypeBinding
			//Text InternalText
			//
			
			//
			//TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeKeyTypeAttribute
			//TaleWorlds.GauntletUI.Data.WidgetAttributeValueTypeBinding
			//Text InternalSetterText
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
					var widgetComponent = _widget_2.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = null;
				}
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
				_widget_0.PropertyChanged -= PropertyChangedListenerOf_widget_0;
				_widget_1.PropertyChanged -= PropertyChangedListenerOf_widget_1;
				_widget_2.PropertyChanged -= PropertyChangedListenerOf_widget_2;
				_widget_3.PropertyChanged -= PropertyChangedListenerOf_widget_3;
				_datasource_Root = null;
			}
		}
		
		public void SetDataSource(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.NonPublicMemberVM dataSource)
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
				if (value is global::System.String uiExtenderExAnnounced__widget_0_Text)
				{
					_datasource_Root.PublicText = uiExtenderExAnnounced__widget_0_Text;
				}
				else if (value == null)
				{
					_datasource_Root.PublicText = default(global::System.String);
				}
				else
				{
					//Announced as another type than the widget property's; written back as the loader writes it
					global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.DynamicMember.Set(_datasource_Root, "PublicText", value);
				}
				return;
			}
		}
		
		private void PropertyChangedListenerOf_widget_1(global::TaleWorlds.GauntletUI.PropertyOwnerObject propertyOwnerObject, global::System.String propertyName, global::System.Object e)
		{
			HandleWidgetPropertyChangeOf_widget_1(propertyName, e);
		}
		
		private void HandleWidgetPropertyChangeOf_widget_1(global::System.String propertyName, global::System.Object value)
		{
			if (propertyName == "Text")
			{
				//Property in ViewModel does not have a set method
				//_datasource_Root.ReadOnlyText = _widget_1.Text;
				return;
			}
		}
		
		private void PropertyChangedListenerOf_widget_2(global::TaleWorlds.GauntletUI.PropertyOwnerObject propertyOwnerObject, global::System.String propertyName, global::System.Object e)
		{
			HandleWidgetPropertyChangeOf_widget_2(propertyName, e);
		}
		
		private void HandleWidgetPropertyChangeOf_widget_2(global::System.String propertyName, global::System.Object value)
		{
			if (propertyName == "Text")
			{
				//Couldn't find property in ViewModel
				//_datasource_Root.InternalText = _widget_2.Text;
				return;
			}
		}
		
		private void PropertyChangedListenerOf_widget_3(global::TaleWorlds.GauntletUI.PropertyOwnerObject propertyOwnerObject, global::System.String propertyName, global::System.Object e)
		{
			HandleWidgetPropertyChangeOf_widget_3(propertyName, e);
		}
		
		private void HandleWidgetPropertyChangeOf_widget_3(global::System.String propertyName, global::System.Object value)
		{
			if (propertyName == "Text")
			{
				//Property in ViewModel does not have a set method
				//_datasource_Root.InternalSetterText = _widget_3.Text;
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
			if (propertyName == "PublicText")
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
			if (propertyName == "ReadOnlyText")
			{
				if (value is global::System.String uiExtenderExAssigned__widget_1_Text)
				{
					try
					{
						_widget_1.Text = uiExtenderExAssigned__widget_1_Text;
					}
					catch (global::System.Exception uiExtenderExSetterFailure__widget_1_Text)
					{
						throw new global::System.Reflection.TargetInvocationException(uiExtenderExSetterFailure__widget_1_Text);
					}
				}
				else
				{
					global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "Text", value);
				}
				return true;
			}
			if (propertyName == "InternalSetterText")
			{
				if (value is global::System.String uiExtenderExAssigned__widget_3_Text)
				{
					try
					{
						_widget_3.Text = uiExtenderExAssigned__widget_3_Text;
					}
					catch (global::System.Exception uiExtenderExSetterFailure__widget_3_Text)
					{
						throw new global::System.Reflection.TargetInvocationException(uiExtenderExSetterFailure__widget_3_Text);
					}
				}
				else
				{
					global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_3, "Text", value);
				}
				return true;
			}
			return false;
		}
		
		private global::System.Boolean HandleViewModelPropertyChangeWithBoolValueOf_datasource_Root(global::System.String propertyName, global::System.Boolean value)
		{
			if (propertyName == "PublicText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_0, "Text", value);
				return true;
			}
			if (propertyName == "ReadOnlyText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "Text", value);
				return true;
			}
			if (propertyName == "InternalSetterText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_3, "Text", value);
				return true;
			}
			return false;
		}
		
		private global::System.Boolean HandleViewModelPropertyChangeWithIntValueOf_datasource_Root(global::System.String propertyName, global::System.Int32 value)
		{
			if (propertyName == "PublicText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_0, "Text", value);
				return true;
			}
			if (propertyName == "ReadOnlyText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "Text", value);
				return true;
			}
			if (propertyName == "InternalSetterText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_3, "Text", value);
				return true;
			}
			return false;
		}
		
		private global::System.Boolean HandleViewModelPropertyChangeWithFloatValueOf_datasource_Root(global::System.String propertyName, global::System.Single value)
		{
			if (propertyName == "PublicText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_0, "Text", value);
				return true;
			}
			if (propertyName == "ReadOnlyText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "Text", value);
				return true;
			}
			if (propertyName == "InternalSetterText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_3, "Text", value);
				return true;
			}
			return false;
		}
		
		private global::System.Boolean HandleViewModelPropertyChangeWithUIntValueOf_datasource_Root(global::System.String propertyName, global::System.UInt32 value)
		{
			if (propertyName == "PublicText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_0, "Text", value);
				return true;
			}
			if (propertyName == "ReadOnlyText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "Text", value);
				return true;
			}
			if (propertyName == "InternalSetterText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_3, "Text", value);
				return true;
			}
			return false;
		}
		
		private global::System.Boolean HandleViewModelPropertyChangeWithColorValueOf_datasource_Root(global::System.String propertyName, global::TaleWorlds.Library.Color value)
		{
			if (propertyName == "PublicText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_0, "Text", value);
				return true;
			}
			if (propertyName == "ReadOnlyText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "Text", value);
				return true;
			}
			if (propertyName == "InternalSetterText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_3, "Text", value);
				return true;
			}
			return false;
		}
		
		private global::System.Boolean HandleViewModelPropertyChangeWithDoubleValueOf_datasource_Root(global::System.String propertyName, global::System.Double value)
		{
			if (propertyName == "PublicText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_0, "Text", value);
				return true;
			}
			if (propertyName == "ReadOnlyText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "Text", value);
				return true;
			}
			if (propertyName == "InternalSetterText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_3, "Text", value);
				return true;
			}
			return false;
		}
		
		private global::System.Boolean HandleViewModelPropertyChangeWithVec2ValueOf_datasource_Root(global::System.String propertyName, global::TaleWorlds.Library.Vec2 value)
		{
			if (propertyName == "PublicText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_0, "Text", value);
				return true;
			}
			if (propertyName == "ReadOnlyText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_1, "Text", value);
				return true;
			}
			if (propertyName == "InternalSetterText")
			{
				global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, _widget_3, "Text", value);
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
			if (propertyName == "PublicText")
			{
				_widget_0.Text = _datasource_Root.PublicText;
				return;
			}
			if (propertyName == "ReadOnlyText")
			{
				_widget_1.Text = _datasource_Root.ReadOnlyText;
				return;
			}
			if (propertyName == "InternalText")
			{
				//Couldn't find property in ViewModel
				//_widget_2.Text = _datasource_Root.InternalText;
				return;
			}
			if (propertyName == "InternalSetterText")
			{
				_widget_3.Text = _datasource_Root.InternalSetterText;
				return;
			}
		}
		
		private void RefreshDataSource_datasource_Root(global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.NonPublicMemberVM newDataSource)
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
				_widget_0.PropertyChanged -= PropertyChangedListenerOf_widget_0;
				_widget_1.PropertyChanged -= PropertyChangedListenerOf_widget_1;
				_widget_2.PropertyChanged -= PropertyChangedListenerOf_widget_2;
				_widget_3.PropertyChanged -= PropertyChangedListenerOf_widget_3;
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
				_widget_0.Text = _datasource_Root.PublicText;
				{
					//Requires component data assignment
					var widgetComponent = _widget_0.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				_widget_0.PropertyChanged += PropertyChangedListenerOf_widget_0;
				_widget_1.Text = _datasource_Root.ReadOnlyText;
				{
					//Requires component data assignment
					var widgetComponent = _widget_1.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				_widget_1.PropertyChanged += PropertyChangedListenerOf_widget_1;
				//Couldn't find property in ViewModel
				//_widget_2.Text = _datasource_Root.InternalText;
				{
					//Requires component data assignment
					var widgetComponent = _widget_2.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				_widget_2.PropertyChanged += PropertyChangedListenerOf_widget_2;
				_widget_3.Text = _datasource_Root.InternalSetterText;
				{
					//Requires component data assignment
					var widgetComponent = _widget_3.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();
					widgetComponent.Data = _datasource_Root;
				}
				_widget_3.PropertyChanged += PropertyChangedListenerOf_widget_3;
			}
		}
		
	}
	
}

