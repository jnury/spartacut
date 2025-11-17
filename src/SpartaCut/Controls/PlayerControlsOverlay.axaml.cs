using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using SpartaCut.Core.ViewModels;

namespace SpartaCut.Controls;

public partial class PlayerControlsOverlay : UserControl
{
    private DispatcherTimer? _fadeTimer;
    private const double IDLE_OPACITY = 0.0;  // Completely hide when idle (like YouTube)
    private const double ACTIVE_OPACITY = 1.0;
    private const int FADE_DELAY_MS = 3000;  // Longer delay for better UX

    public PlayerControlsOverlay()
    {
        InitializeComponent();

        // Setup fade timer
        _fadeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(FADE_DELAY_MS)
        };
        _fadeTimer.Tick += OnFadeTimerTick;

        // Track mouse movement
        // Note: PointerMoved events are propagated from parent MainWindow
        // Don't subscribe to self to avoid infinite event loop
        this.PointerEntered += OnPointerEntered;
        this.PointerExited += OnPointerExited;

        // Start with controls visible
        SetActiveOpacity();
    }

    public void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        // Reset fade timer on mouse movement
        SetActiveOpacity();
        _fadeTimer?.Stop();

        // Only start fade timer if video is NOT playing
        // (keep controls visible during playback)
        if (!IsVideoPlaying())
        {
            _fadeTimer?.Start();
        }
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        SetActiveOpacity();
        _fadeTimer?.Stop();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        // Only start fade timer if video is NOT playing
        if (!IsVideoPlaying())
        {
            _fadeTimer?.Start();
        }
    }

    private void OnFadeTimerTick(object? sender, EventArgs e)
    {
        _fadeTimer?.Stop();

        // Only fade if video is NOT playing
        if (!IsVideoPlaying())
        {
            SetIdleOpacity();
        }
    }

    private bool IsVideoPlaying()
    {
        // Check if video is currently playing via ViewModel
        return DataContext is MainWindowViewModel viewModel && viewModel.IsPlaying;
    }

    private void SetActiveOpacity()
    {
        if (this.FindControl<Border>("ControlsContainer") is Border container)
        {
            AnimateOpacity(container, ACTIVE_OPACITY);
            container.IsHitTestVisible = true; // Enable clicks when visible
        }
    }

    private void SetIdleOpacity()
    {
        if (this.FindControl<Border>("ControlsContainer") is Border container)
        {
            AnimateOpacity(container, IDLE_OPACITY);
            container.IsHitTestVisible = false; // Allow clicks through when faded
        }
    }

    private void AnimateOpacity(Border target, double toOpacity)
    {
        target.Opacity = toOpacity;
    }
}
