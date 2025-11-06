namespace Bref.Models;

/// <summary>
/// Represents the current state of video playback
/// </summary>
public enum PlaybackState
{
    /// <summary>
    /// Playback is stopped (at start or end)
    /// </summary>
    Stopped,

    /// <summary>
    /// Playback is paused
    /// </summary>
    Paused,

    /// <summary>
    /// Playback is actively playing
    /// </summary>
    Playing
}

/// <summary>
/// Extension methods for PlaybackState
/// </summary>
public static class PlaybackStateExtensions
{
    /// <summary>
    /// Returns true if playback is actively playing
    /// </summary>
    public static bool IsPlaying(this PlaybackState state)
    {
        return state == PlaybackState.Playing;
    }
}
