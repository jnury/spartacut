using System;
using Xunit;
using Bref.Services;

namespace Bref.Tests.Services;

public class VlcPlaybackEngineTests
{
    [Fact]
    public void Constructor_InitializesLibVLC()
    {
        // Act
        using var engine = new VlcPlaybackEngine();

        // Assert - Should not throw
        Assert.NotNull(engine);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var engine = new VlcPlaybackEngine();

        // Act & Assert - Should not throw
        engine.Dispose();
    }
}
