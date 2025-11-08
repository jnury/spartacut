using Bref.Core.ViewModels;
using Bref.Tests.Mocks;
using Xunit;

namespace Bref.Tests.Services;

public class VlcPlaybackEngineVolumeTests
{
    [Fact]
    public void SetVolume_WithValidValue_UpdatesVolume()
    {
        // Arrange
        var engine = new MockPlaybackEngine();

        // Act
        engine.SetVolume(0.5f);

        // Assert
        Assert.Equal(0.5f, engine.Volume);
    }

    [Fact]
    public void SetVolume_WithValueAbove1_ClampsTo1()
    {
        // Arrange
        var engine = new MockPlaybackEngine();

        // Act
        engine.SetVolume(1.5f);

        // Assert
        Assert.Equal(1.0f, engine.Volume);
    }

    [Fact]
    public void SetVolume_WithNegativeValue_ClampsTo0()
    {
        // Arrange
        var engine = new MockPlaybackEngine();

        // Act
        engine.SetVolume(-0.5f);

        // Assert
        Assert.Equal(0.0f, engine.Volume);
    }

    [Fact]
    public void Volume_InitialValue_Is100Percent()
    {
        // Arrange & Act
        var engine = new MockPlaybackEngine();

        // Assert
        Assert.Equal(1.0f, engine.Volume);
    }

    [Fact]
    public void MainWindowViewModel_VolumeProperty_UpdatesPlaybackEngine()
    {
        // Arrange
        var engine = new MockPlaybackEngine();
        var viewModel = new MainWindowViewModel(engine);

        // Act
        viewModel.Volume = 0.75;

        // Assert
        Assert.Equal(0.75f, engine.Volume);
    }
}
