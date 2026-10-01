using Microsoft.Extensions.Logging;

namespace NgoFund.Desktop.Services;

/// <summary>
/// Holds the Google Form unread/pending counts for the Applications screen's badges. A thin cache
/// over <see cref="ApiClient.GetIntakeSummaryAsync"/>/<see cref="ApiClient.MarkIntakeSeenAsync"/> —
/// never throws to callers, since a badge failing to update is not worth interrupting the user's
/// work for.
/// </summary>
public class IntakeNotificationState : IDisposable
{
    private readonly ApiClient _apiClient;
    private readonly AuthState _authState;
    private readonly ILogger<IntakeNotificationState> _logger;

    public IntakeNotificationState(ApiClient apiClient, AuthState authState, ILogger<IntakeNotificationState> logger)
    {
        _apiClient = apiClient;
        _authState = authState;
        _logger = logger;
        _authState.Changed += OnAuthStateChanged;
    }

    public int UnreadCount { get; private set; }

    public int PendingCount { get; private set; }

    public bool HasLoaded { get; private set; }

    /// <summary>Raised after the counts change. May fire off the UI thread (it's driven by HTTP
    /// completions and <see cref="AuthState.Changed"/>, not a Blazor render) — subscribers must
    /// wrap their handling in <c>InvokeAsync</c>.</summary>
    public event Action? Changed;

    public async Task RefreshAsync()
    {
        if (!_authState.IsAuthenticated || !_authState.HasPermission("applications.view"))
        {
            return;
        }

        try
        {
            var summary = await _apiClient.GetIntakeSummaryAsync();
            UnreadCount = summary.UnreadGoogleFormCount;
            PendingCount = summary.PendingGoogleFormCount;
            HasLoaded = true;
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh Google Form intake summary.");
        }
    }

    public async Task<int> MarkSeenAsync()
    {
        try
        {
            var result = await _apiClient.MarkIntakeSeenAsync();
            await RefreshAsync();
            return result.ClearedUnreadCount;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mark Google Form intake seen.");
            return 0;
        }
    }

    private void OnAuthStateChanged()
    {
        if (_authState.IsAuthenticated)
        {
            return;
        }

        UnreadCount = 0;
        PendingCount = 0;
        HasLoaded = false;
        Changed?.Invoke();
    }

    public void Dispose() => _authState.Changed -= OnAuthStateChanged;
}
