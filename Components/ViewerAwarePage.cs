using Microsoft.AspNetCore.Components;
using VillageCrm.Services;

namespace VillageCrm.Components;

/// <summary>
/// Base class for pages that show different content depending on the viewer's role.
/// Re-renders the page (and reloads via <see cref="OnViewerChangedAsync"/>) when the viewer changes.
/// </summary>
public abstract class ViewerAwarePage : ComponentBase, IDisposable
{
    [Inject] protected ViewerContext Viewer { get; set; } = default!;

    /// <summary>Message from the last action that failed a business rule; shown by the page.</summary>
    protected string? Error { get; set; }

    protected override void OnInitialized()
    {
        Viewer.Changed += OnViewerChanged;
    }

    protected virtual Task OnViewerChangedAsync() => Task.CompletedTask;

    private void OnViewerChanged() => _ = InvokeAsync(async () =>
    {
        await OnViewerChangedAsync();
        StateHasChanged();
    });

    /// <summary>Runs an action and shows rule violations as a friendly message instead of crashing the page.</summary>
    protected async Task<bool> TryAsync(Func<Task> action)
    {
        Error = null;
        try
        {
            await action();
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            Error = ex.Message;
            return false;
        }
    }

    public void Dispose()
    {
        Viewer.Changed -= OnViewerChanged;
    }
}
