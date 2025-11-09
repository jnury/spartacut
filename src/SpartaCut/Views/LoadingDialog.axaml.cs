using Avalonia.Controls;
using Avalonia.Threading;
using SpartaCut.Core.Models;

namespace SpartaCut.Views;

public partial class LoadingDialog : Window
{
    public LoadingDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates the loading dialog with progress information.
    /// Thread-safe - can be called from any thread.
    /// </summary>
    public void UpdateProgress(LoadProgress progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusTextBlock.Text = progress.Message;
            LoadProgressBar.Value = progress.Percentage;
            PercentageTextBlock.Text = $"{progress.Percentage}%";

            if (progress.IsComplete || progress.IsFailed)
            {
                Close();
            }
        });
    }
}
