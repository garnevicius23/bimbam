namespace BimBam.Core.Interfaces;

/// <summary>
/// Runs work on the thread that owns the UI-bound session objects, so changes arriving from the
/// network never race with the UI reading the same session.
/// </summary>
public interface IMainThreadDispatcher
{
    Task InvokeAsync(Action action);

    Task<T> InvokeAsync<T>(Func<T> func);
}
