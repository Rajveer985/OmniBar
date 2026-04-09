using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WinSpotlight;

public partial class MainWindow : Window
{
    private readonly HotkeyManager    _hotkey    = new();
    private          ClipboardManager? _clipboard;
    private          SearchEngine?     _engine;
    private          CancellationTokenSource _cts = new();

    // ── Constructor ──────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Register global hotkey: Alt + Space  (VK_SPACE = 0x20)
        bool ok = _hotkey.Register(this, HotkeyManager.MOD_ALT, 0x20);
        if (!ok)
        {
            // Fallback: try Alt+` if Alt+Space is taken
            _hotkey.Register(this, HotkeyManager.MOD_ALT, 0xC0);
        }
        _hotkey.HotkeyPressed += ShowSpotlight;

        // Clipboard monitor (piggybacks on this window's HWND)
        _clipboard = new ClipboardManager(this);

        // Search engine
        _engine = new SearchEngine(_clipboard);

        // Hide until hotkey is pressed
        Hide();
    }

    // ── Show / Hide ───────────────────────────────────────────────────────────

    public void ShowSpotlight()
    {
        Dispatcher.Invoke(() =>
        {
            ResetUI();
            PositionOnActiveMonitor();
            
            // Ensure initial state for animation
            Card.Opacity = 0;
            CardScale.ScaleX = 0.96;
            CardScale.ScaleY = 0.96;

            Show();
            Activate();
            SearchBox.Focus();

            if (FindResource("ShowAnim") is Storyboard showAnim)
            {
                showAnim.Begin();
            }
        });
    }

    private async void HideSpotlight()
    {
        if (Visibility != Visibility.Visible) return;

        // Play hide animation first
        if (FindResource("HideAnim") is Storyboard hideAnim)
        {
            var tcs = new TaskCompletionSource<bool>();
            EventHandler onCompleted = null!;
            onCompleted = (s, e) => 
            {
                hideAnim.Completed -= onCompleted;
                tcs.SetResult(true);
            };
            hideAnim.Completed += onCompleted;
            hideAnim.Begin();
            
            // Wait for animation or timeout (just in case)
            await Task.WhenAny(tcs.Task, Task.Delay(250));
        }

        Hide();
        ResetUI();
    }

    private void ResetUI()
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();

        SearchBox.Text           = "";
        ResultsList.ItemsSource  = null;
        
        ResultsPanel.Visibility  = Visibility.Collapsed;
        ResultsPanel.Opacity     = 0;
        ResultsTranslate.Y       = -10;

        ClearBtn.Visibility      = Visibility.Collapsed;
    }

    // ── Positioning ───────────────────────────────────────────────────────────

    private void PositionOnActiveMonitor()
    {
        // Use the monitor that contains the mouse cursor
        var pt     = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(pt).WorkingArea;

        // DPI-aware: WPF uses logical units (96 dpi baseline)
        var source     = PresentationSource.FromVisual(this);
        double dpiX    = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        double dpiY    = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        double screenW = screen.Width  * dpiX;
        double screenH = screen.Height * dpiY;
        double screenL = screen.Left   * dpiX;
        double screenT = screen.Top    * dpiY;

        Left = screenL + (screenW - Width)  / 2;
        Top  = screenT + screenH * 0.16;
    }

    // ── Search ─────────────────────────────────────────────────────────────────

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text;

        ClearBtn.Visibility = query.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Cancel previous search
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            await Task.Delay(130, token); // debounce 130 ms

            if (_engine == null) return;
            var results = await _engine.SearchAsync(query, token);

            if (token.IsCancellationRequested) return;

            bool hasResults = results.Count > 0;
            ResultsList.ItemsSource = results;
            
            if (hasResults)
            {
                ResultsList.SelectedIndex = 0;
                ShowResultsPanel();
            }
            else
            {
                HideResultsPanel();
            }
        }
        catch (OperationCanceledException) { /* normal debounce cancel */ }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Search] {ex.Message}");
        }
    }

    // ── Keyboard navigation ────────────────────────────────────────────────────

    private void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                HideSpotlight();
                e.Handled = true;
                break;

            case Key.Enter:
                LaunchSelected();
                e.Handled = true;
                break;

            case Key.Down:
                if (ResultsList.Items.Count > 0)
                {
                    ResultsList.Focus();
                    ResultsList.SelectedIndex = Math.Min(
                        ResultsList.SelectedIndex + 1, ResultsList.Items.Count - 1);
                    ScrollToSelected();
                }
                e.Handled = true;
                break;

            case Key.Up:
                // Up from top of list → return focus to SearchBox
                if (ResultsList.SelectedIndex > 0)
                {
                    ResultsList.Focus();
                    ResultsList.SelectedIndex--;
                    ScrollToSelected();
                }
                e.Handled = true;
                break;
        }
    }

    private void ShowResultsPanel()
    {
        if (ResultsPanel.Visibility == Visibility.Visible && ResultsPanel.Opacity > 0.9) return;

        ResultsPanel.Visibility = Visibility.Visible;
        
        var duration = TimeSpan.FromSeconds(0.25);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var animOpacity = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
        var animTranslate = new DoubleAnimation(-12, 0, duration) { EasingFunction = ease };

        ResultsPanel.BeginAnimation(OpacityProperty, animOpacity);
        ResultsTranslate.BeginAnimation(TranslateTransform.YProperty, animTranslate);
    }

    private void HideResultsPanel()
    {
        ResultsPanel.Visibility = Visibility.Collapsed;
        ResultsPanel.Opacity = 0;
        ResultsTranslate.Y = -12;
    }

    private void ResultsList_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                HideSpotlight();
                e.Handled = true;
                break;

            case Key.Enter:
                LaunchSelected();
                e.Handled = true;
                break;

            case Key.Up when ResultsList.SelectedIndex == 0:
                // Jump back to search box
                SearchBox.Focus();
                SearchBox.CaretIndex = SearchBox.Text.Length;
                e.Handled = true;
                break;

            default:
                // Any printable key while list is focused → refocus search box
                if (e.Key >= Key.A && e.Key <= Key.Z ||
                    e.Key >= Key.D0 && e.Key <= Key.D9 ||
                    e.Key == Key.Space || e.Key == Key.Back)
                {
                    SearchBox.Focus();
                }
                break;
        }
    }

    private void ScrollToSelected()
    {
        if (ResultsList.SelectedItem != null)
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    // ── Mouse ──────────────────────────────────────────────────────────────────

    private void ResultsList_Click(object sender, MouseButtonEventArgs e)
    {
        // Single-click launches the item the user clicked
        if (e.OriginalSource is FrameworkElement fe &&
            fe.DataContext is SearchResult)
        {
            LaunchSelected();
        }
    }

    private void ClearBtn_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    // ── Window events ──────────────────────────────────────────────────────────

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Auto-hide when another window takes focus
        HideSpotlight();
    }

    // ── Launch ─────────────────────────────────────────────────────────────────

    private void LaunchSelected()
    {
        if (ResultsList.SelectedItem is SearchResult result)
        {
            HideSpotlight();
            Launch(result);
        }
    }

    private static void Launch(SearchResult result)
    {
        try
        {
            switch (result.Category)
            {
                case ResultCategory.Math:
                    // Copy the numeric result to the clipboard
                    System.Windows.Clipboard.SetText(result.ActionPath);
                    break;

                case ResultCategory.Clipboard:
                    // Re-copy the historical entry
                    System.Windows.Clipboard.SetText(result.ActionPath);
                    break;

                case ResultCategory.Timer:
                    // Launch Windows Clock app with duration
                    string uri = "ms-clock:timer";
                    if (!string.IsNullOrEmpty(result.ActionPath))
                        uri += $"?duration={result.ActionPath}";
                        
                    Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
                    break;

                case ResultCategory.Calendar:
                    // Open Windows Calendar app
                    Process.Start(new ProcessStartInfo("outlookcal:") { UseShellExecute = true });
                    break;

                default:
                    // App, File, Web — open with shell
                    Process.Start(new ProcessStartInfo(result.ActionPath)
                    {
                        UseShellExecute = true
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Could not open:\n{result.ActionPath}\n\n{ex.Message}",
                            "WinSpotlight", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ── Cleanup ────────────────────────────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        _hotkey.Dispose();
        _clipboard?.Dispose();
        base.OnClosed(e);
    }
}
