using System.Windows;
using BimBam.Core.Interfaces;

namespace BimBam.App.Services;

/// <summary>Runs work on the WPF UI thread, where all on-screen session objects live.</summary>
public sealed class WpfMainThreadDispatcher : IMainThreadDispatcher
{
    public Task InvokeAsync(Action action)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        var dispatcher = Application.Current.Dispatcher;
        return dispatcher.CheckAccess()
            ? Task.FromResult(func())
            : dispatcher.InvokeAsync(func).Task;
    }
}
