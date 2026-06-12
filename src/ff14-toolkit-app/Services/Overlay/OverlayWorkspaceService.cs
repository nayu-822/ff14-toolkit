using FF14Toolkit.App.Models.Overlay;
using FF14Toolkit.App.Services.Crafting;
using FF14Toolkit.App.ViewModels;
using FF14Toolkit.App.Views;
using System.Windows;
using System.Windows.Threading;

namespace FF14Toolkit.App.Services.Overlay;

public sealed class OverlayWorkspaceService
{
    private const double DefaultWindowWidth = 320;
    private const double DefaultWindowHeight = 220;
    private const double DefaultMargin = 24;

    private readonly OverlayLayoutStore layoutStore;
    private readonly CraftSequenceOverlayStateService craftSequenceOverlayStateService;
    private readonly DispatcherTimer persistTimer;
    private readonly Dictionary<string, OverlayWindowLayout> layoutsById;
    private readonly List<OverlayWindowHost> overlayWindows;
    private Window? mainWindow;

    public event EventHandler? StateChanged;

    public OverlayWorkspaceService(
        OverlayLayoutStore layoutStore,
        CraftSequenceOverlayStateService craftSequenceOverlayStateService)
    {
        this.layoutStore = layoutStore;
        this.craftSequenceOverlayStateService = craftSequenceOverlayStateService;
        layoutsById = layoutStore.LoadLayouts()
            .Where(IsValidLayout)
            .ToDictionary(layout => layout.WindowId, StringComparer.OrdinalIgnoreCase);
        overlayWindows = [];
        persistTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        persistTimer.Tick += OnPersistTimerTick;
    }

    public bool IsOverlayMode { get; private set; }

    public bool IsEditMode { get; private set; }

    public void Initialize(Window window)
    {
        mainWindow = window ?? throw new ArgumentNullException(nameof(window));
        EnsureOverlayWindows();
    }

    public void ToggleMode()
    {
        if (mainWindow is null)
        {
            return;
        }

        if (IsOverlayMode)
        {
            ShowForegroundWindow();
            return;
        }

        ShowOverlayWindows();
    }

    public void ToggleEditMode()
    {
        IsEditMode = !IsEditMode;

        foreach (OverlayWindowHost host in overlayWindows)
        {
            host.Window.SetEditMode(IsEditMode);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Shutdown()
    {
        PersistLayouts();

        foreach (OverlayWindowHost host in overlayWindows)
        {
            host.Window.CloseOverlay();
        }
    }

    private void EnsureOverlayWindows()
    {
        if (overlayWindows.Count > 0)
        {
            return;
        }

        foreach (OverlayWindowDefinition definition in CreateDefinitions())
        {
            OverlayWindow window = new(new OverlayWindowViewModel(craftSequenceOverlayStateService));
            window.LayoutChanged += OnOverlayWindowLayoutChanged;
            window.SetEditMode(IsEditMode);
            ApplyLayout(window, definition);
            overlayWindows.Add(new OverlayWindowHost(definition, window));
        }
    }

    private void ShowOverlayWindows()
    {
        if (mainWindow is null)
        {
            return;
        }

        EnsureOverlayWindows();

        foreach (OverlayWindowHost host in overlayWindows)
        {
            ApplyLayout(host.Window, host.Definition);
            host.Window.SetEditMode(IsEditMode);
            host.Window.Show();
            host.Window.Activate();
        }

        mainWindow.Hide();
        IsOverlayMode = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowForegroundWindow()
    {
        if (mainWindow is null)
        {
            return;
        }

        foreach (OverlayWindowHost host in overlayWindows)
        {
            host.Window.Hide();
        }

        PersistLayouts();

        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Show();
        mainWindow.Activate();
        mainWindow.Topmost = true;
        mainWindow.Topmost = false;

        IsOverlayMode = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyLayout(OverlayWindow window, OverlayWindowDefinition definition)
    {
        OverlayWindowLayout layout;
        if (layoutsById.TryGetValue(definition.WindowId, out OverlayWindowLayout? savedLayout))
        {
            layout = savedLayout;
        }
        else
        {
            layout = CreateDefaultLayout(definition.Anchor);
            layout.WindowId = definition.WindowId;
            layoutsById[definition.WindowId] = layout;
        }

        window.ApplyLayout(layout);
    }

    private OverlayWindowLayout CreateDefaultLayout(OverlayAnchor anchor)
    {
        Rect workArea = SystemParameters.WorkArea;

        double left = workArea.Left + DefaultMargin;
        double top = workArea.Top + DefaultMargin;

        switch (anchor)
        {
            case OverlayAnchor.TopRight:
                left = workArea.Right - DefaultWindowWidth - DefaultMargin;
                break;
            case OverlayAnchor.BottomRight:
                left = workArea.Right - DefaultWindowWidth - DefaultMargin;
                top = workArea.Bottom - DefaultWindowHeight - DefaultMargin;
                break;
        }

        return new OverlayWindowLayout
        {
            Left = left,
            Top = top,
            Width = DefaultWindowWidth,
            Height = DefaultWindowHeight
        };
    }

    private void OnOverlayWindowLayoutChanged(object? sender, EventArgs e)
    {
        if (sender is not OverlayWindow window)
        {
            return;
        }

        OverlayWindowHost? host = overlayWindows.FirstOrDefault(candidate => ReferenceEquals(candidate.Window, window));
        if (host is null)
        {
            return;
        }

        OverlayWindowLayout layout = window.CaptureLayout();
        layout.WindowId = host.Definition.WindowId;
        layoutsById[layout.WindowId] = layout;
        SchedulePersist();
    }

    private void SchedulePersist()
    {
        persistTimer.Stop();
        persistTimer.Start();
    }

    private void OnPersistTimerTick(object? sender, EventArgs e)
    {
        persistTimer.Stop();
        PersistLayouts();
    }

    private void PersistLayouts()
    {
        layoutStore.SaveLayouts(layoutsById.Values.OrderBy(layout => layout.WindowId, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsValidLayout(OverlayWindowLayout layout)
    {
        return !string.IsNullOrWhiteSpace(layout.WindowId)
            && double.IsFinite(layout.Left)
            && double.IsFinite(layout.Top)
            && double.IsFinite(layout.Width)
            && double.IsFinite(layout.Height)
            && layout.Width > 0
            && layout.Height > 0;
    }

    private static IReadOnlyList<OverlayWindowDefinition> CreateDefinitions()
    {
        return
        [
            new OverlayWindowDefinition("craft-sequence-overlay", OverlayAnchor.TopRight)
        ];
    }

    private sealed record OverlayWindowDefinition(string WindowId, OverlayAnchor Anchor);

    private sealed record OverlayWindowHost(OverlayWindowDefinition Definition, OverlayWindow Window);

    private enum OverlayAnchor
    {
        TopLeft,
        TopRight,
        BottomRight
    }
}
