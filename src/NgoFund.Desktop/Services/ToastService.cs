namespace NgoFund.Desktop.Services;

public enum ToastKind
{
    Success,
    Error,
}

public sealed record ToastMessage(Guid Id, ToastKind Kind, string Text);

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

    private void Add(ToastKind kind, string text)
    {
        var toast = new ToastMessage(Guid.NewGuid(), kind, text);
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
