using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace NgoFund.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // BlazorWebView's underlying WebView2 child control (the .WebView property) doesn't
        // exist yet right after InitializeComponent() — it's created later in the control's own
        // lifecycle — so wiring must wait for Loaded, when the visual tree (including that child
        // control) is guaranteed to be fully constructed.
        BlazorWebViewControl.Loaded += OnBlazorWebViewLoaded;
    }

    private void OnBlazorWebViewLoaded(object sender, RoutedEventArgs e)
    {
        BlazorWebViewControl.Loaded -= OnBlazorWebViewLoaded;
        BlazorWebViewControl.WebView.CoreWebView2InitializationCompleted += OnCoreWebView2InitializationCompleted;
    }

    // WebView2 denies camera/microphone getUserMedia() requests by default unless the host
    // explicitly grants them here — without this, WebcamCapture's navigator.mediaDevices call
    // rejects immediately, which is what surfaced as "webcam capture gives an error".
    private void OnCoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess || BlazorWebViewControl.WebView.CoreWebView2 is null)
        {
            return;
        }

        BlazorWebViewControl.WebView.CoreWebView2.PermissionRequested += (_, args) =>
        {
            if (args.PermissionKind is CoreWebView2PermissionKind.Camera or CoreWebView2PermissionKind.Microphone)
            {
                args.State = CoreWebView2PermissionState.Allow;
            }
        };
    }
}
