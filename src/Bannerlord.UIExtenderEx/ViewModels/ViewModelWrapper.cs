using Bannerlord.UIExtenderEx.Extensions;

using System.ComponentModel;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.ViewModels
{
    // TODO: Is there a use for that?
    internal class ViewModelWrapper : ViewModel
    {
        public ViewModel? Object { get; }

        protected ViewModelWrapper(ViewModel @object)
        {
            Object = @object;

            foreach (var property in this.GetViewModelProperties())
            {
                this.AddProperty(property.Name, property);
            }
            foreach (var method in this.GetViewModelMethods())
            {
                this.AddMethod(method.Name, method);
            }

            // Trigger OnPropertyChanged from Object
            if (Object is IViewModel viewModel)
                viewModel.PropertyChangedWithValue += OnPropertyChangedWithValueEventHandler;
            else if (Object is INotifyPropertyChanged notifyPropertyChanged)
                notifyPropertyChanged.PropertyChanged += OnPropertyChangedEventHandler;
        }

        private void OnPropertyChangedEventHandler(object? sender, PropertyChangedEventArgs args)
        {
            OnPropertyChanged(args.PropertyName);
        }
        private void OnPropertyChangedWithValueEventHandler(object? sender, PropertyChangedWithValueEventArgs args)
        {
            OnPropertyChangedWithValue(args.Value, args.PropertyName);
        }

        public override void RefreshValues()
        {
            Object?.RefreshValues();

            base.RefreshValues();
        }

        public override void OnFinalize()
        {
            if (Object is not null)
            {
                Object.PropertyChanged -= OnPropertyChangedEventHandler;
                Object.PropertyChangedWithValue -= OnPropertyChangedWithValueEventHandler;

                Object.OnFinalize();
            }

            base.OnFinalize();
        }
    }
}