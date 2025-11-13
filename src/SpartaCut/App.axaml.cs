using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;

namespace SpartaCut;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Views.MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private Views.MainWindow? GetMainWindow()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow as Views.MainWindow;
        }
        return null;
    }

    private void AboutMenuItem_Click(object? sender, EventArgs e)
    {
        var mainWindow = GetMainWindow();
        mainWindow?.ShowAboutDialogPublic();
    }

    private void OpenVideoMenuItem_Click(object? sender, EventArgs e)
    {
        var mainWindow = GetMainWindow();
        mainWindow?.TriggerLoadVideo();
    }

    private void SaveAsMenuItem_Click(object? sender, EventArgs e)
    {
        var mainWindow = GetMainWindow();
        if (mainWindow != null && mainWindow.ViewModel?.ExportCommand.CanExecute(null) == true)
        {
            mainWindow.ViewModel.ExportCommand.Execute(null);
        }
    }

    private void UndoMenuItem_Click(object? sender, EventArgs e)
    {
        var mainWindow = GetMainWindow();
        if (mainWindow != null && mainWindow.ViewModel?.UndoCommand.CanExecute(null) == true)
        {
            mainWindow.ViewModel.UndoCommand.Execute(null);
        }
    }

    private void RedoMenuItem_Click(object? sender, EventArgs e)
    {
        var mainWindow = GetMainWindow();
        if (mainWindow != null && mainWindow.ViewModel?.RedoCommand.CanExecute(null) == true)
        {
            mainWindow.ViewModel.RedoCommand.Execute(null);
        }
    }

    private void QuitMenuItem_Click(object? sender, EventArgs e)
    {
        var mainWindow = GetMainWindow();
        mainWindow?.Close();
    }
}
