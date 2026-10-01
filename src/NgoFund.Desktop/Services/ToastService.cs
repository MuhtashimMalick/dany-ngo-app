namespace NgoFund.Desktop.Services;

public enum ToastKind
{
    Success,
    Error,
    Info,
}

/// <summary><see cref="OnClick"/> is optional — e.g. the intake poller's Info toast (F3) navigates
/// to the pre-filtered Applications list when clicked; Success/Error toasts never set it.</summary>
public sealed record ToastMessage(Guid Id, ToastKind Kind, string Text, Action? OnClick = null);

/// <summary>
/// Backing store for Toast.razor. Singleton (one BlazorWebView per app, like AuthState) so any
/// page/component can fire a toast without needing one mounted locally. Pages call
/// <see cref="Success"/> after create/update/void/status-change actions; the inline `.alert`
/// element stays reserved for form-level validation/API failures per the punch list.
/// </summary>
public sealed class ToastService
{
    private const int AutoDismissMs = 4000;

    public event Action? Changed;

    public List<ToastMessage> Items { get; } = [];

    public void Success(string text) => Add(ToastKind.Success, text);

    public void Error(string text) => Add(ToastKind.Error, text);

    /// <summary>Non-urgent, non-error notifications — e.g. the intake poller's "N Google Form
    /// application(s) awaiting review" (F3). Same styling/auto-dismiss as Success/Error, just a
    /// neutral/informational icon and color instead of green/red. <paramref name="onClick"/> is
    /// optional — when set, Toast.razor renders the toast as clickable and invokes it (then
    /// dismisses) on click, without disturbing the separate dismiss ("x") button.</summary>
    public void Info(string text, Action? onClick = null) => Add(ToastKind.Info, text, onClick);

    private void Add(ToastKind kind, string text, Action? onClick = null)
    {
        var toast = new ToastMessage(Guid.NewGuid(), kind, text, onClick);
        Items.Add(toast);
        Changed?.Invoke();

        _ = DismissAfterDelayAsync(toast.Id);
    }

    private async Task DismissAfterDelayAsync(Guid id)
    {
        await Task.Delay(AutoDismissMs);
        Dismiss(id);
    }

    public void Dismiss(Guid id)
    {
        if (Items.RemoveAll(t => t.Id == id) > 0)
        {
            Changed?.Invoke();
        }
    }
}
