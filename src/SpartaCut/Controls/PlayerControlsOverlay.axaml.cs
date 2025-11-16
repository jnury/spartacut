using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace SpartaCut.Controls;

public partial class PlayerControlsOverlay : UserControl
{
    private DispatcherTimer? _fadeTimer;
    private const double IDLE_OPACITY = 0.5;
    private const double ACTIVE_OPACITY = 1.0;
    private const int FADE_DELAY_MS = 2000;

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
        this.PointerMoved += OnPointerMoved;
        this.PointerEntered += OnPointerEntered;
        this.PointerExited += OnPointerExited;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        // Reset fade timer on mouse movement
        SetActiveOpacity();
        _fadeTimer?.Stop();
        _fadeTimer?.Start();
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        SetActiveOpacity();
        _fadeTimer?.Stop();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _fadeTimer?.Start();
    }

    private void OnFadeTimerTick(object? sender, EventArgs e)
    {
        _fadeTimer?.Stop();
        SetIdleOpacity();
    }

    private void SetActiveOpacity()
    {
        if (this.FindControl<Border>("ControlsContainer") is Border container)
        {
            AnimateOpacity(container, ACTIVE_OPACITY);
        }
    }

    private void SetIdleOpacity()
    {
        if (this.FindControl<Border>("ControlsContainer") is Border container)
        {
            AnimateOpacity(container, IDLE_OPACITY);
        }
    }

    private void AnimateOpacity(Border target, double toOpacity)
    {
        target.Opacity = toOpacity;
    }
}
