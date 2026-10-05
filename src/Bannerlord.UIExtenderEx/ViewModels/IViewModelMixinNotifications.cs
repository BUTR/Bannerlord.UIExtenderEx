namespace Bannerlord.UIExtenderEx.ViewModels;

/// <summary>
/// Defines internal lifecycle hooks for attaching and detaching mixin listeners to host ViewModel property change events.
/// <para>
/// Subscribed at the completion of host ViewModel construction and unsubscribed upon host finalization.
/// </para>
/// </summary>
internal interface IViewModelMixinNotifications
{
    void Subscribe();

    void Unsubscribe();
}