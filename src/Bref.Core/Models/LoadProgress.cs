namespace Bref.Core.Models;

/// <summary>
/// Progress information for video loading operations.
/// </summary>
public class LoadProgress
{
    /// <summary>
    /// Current stage of loading process.
    /// </summary>
    public LoadStage Stage { get; init; }

    /// <summary>
    /// Progress percentage for current stage (0-100).
    /// </summary>
    public int Percentage { get; init; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Whether the operation has completed.
    /// </summary>
    public bool IsComplete => Stage == LoadStage.Complete;

    /// <summary>
    /// Whether the operation has failed.
    /// </summary>
    public bool IsFailed => Stage == LoadStage.Failed;
}

/// <summary>
/// Stages of video loading process.
/// </summary>
public enum LoadStage
{
    Validating,
    ExtractingMetadata,
    GeneratingWaveform,
    Complete,
    Failed
}
