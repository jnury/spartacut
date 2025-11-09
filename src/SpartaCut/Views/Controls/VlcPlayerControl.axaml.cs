using Avalonia.Controls;
using LibVLCSharp.Shared;
using Serilog;

namespace SpartaCut.Controls;

public partial class VlcPlayerControl : UserControl
{
    public VlcPlayerControl()
    {
        InitializeComponent();
        Log.Debug("VlcPlayerControl initialized");
    }

    /// <summary>
    /// Binds a MediaPlayer to the VideoView
    /// </summary>
    public void SetMediaPlayer(MediaPlayer mediaPlayer)
    {
        VideoView.MediaPlayer = mediaPlayer;
        Log.Information("MediaPlayer bound to VideoView");
    }
}
