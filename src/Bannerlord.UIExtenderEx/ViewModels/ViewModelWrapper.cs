using Bannerlord.UIExtenderEx.Extensions;

using System.ComponentModel;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.ViewModels;

/// <summary>
/// Wraps an underlying <see cref="ViewModel"/> instance, forwarding property change notifications and copying member bindings.
/// </summary>
internal class ViewModelWrapper : ViewModel
{
    public ViewModel? Object { get; }

    protected ViewModelWrapper(ViewModel @object)
    {
        Object = @object;

        // Publish a single unified binding table; see ViewModelExtensions.BeginRegistration
        using (var registration = this.BeginRegistration())
        {
            foreach (var property in this.GetViewModelProperties())
            {
                registration.AddProperty(property.Name, property);
            }
            foreach (var method in this.GetViewModelMethods())
            {
                registration.AddMethod(method.Name, method);
            }
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