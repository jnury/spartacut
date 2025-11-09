using SpartaCut.Core.Models;
using Xunit;

namespace SpartaCut.Tests.Models
{
    public class TimelineSelectionTests
    {
        [Fact]
        public void Constructor_InitializesWithNoSelection()
        {
            // Arrange & Act
            var selection = new TimelineSelection();

            // Assert
            Assert.False(selection.IsActive);
            Assert.Equal(TimeSpan.Zero, selection.SelectionStart);
            Assert.Equal(TimeSpan.Zero, selection.SelectionEnd);
        }

        [Fact]
        public void StartSelection_SetsStartTime()
        {
            // Arrange
            var selection = new TimelineSelection();
            var startTime = TimeSpan.FromSeconds(10);

            // Act
            selection.StartSelection(startTime);

            // Assert
            Assert.True(selection.IsActive);
            Assert.Equal(startTime, selection.SelectionStart);
            Assert.Equal(startTime, selection.SelectionEnd);
        }

        [Fact]
        public void UpdateSelection_SetsEndTime()
        {
            // Arrange
            var selection = new TimelineSelection();
            var startTime = TimeSpan.FromSeconds(10);
            var endTime = TimeSpan.FromSeconds(20);

            selection.StartSelection(startTime);

            // Act
            selection.UpdateSelection(endTime);

            // Assert
            Assert.Equal(endTime, selection.SelectionEnd);
            Assert.Equal(startTime, selection.SelectionStart);
        }

        [Fact]
        public void Duration_CalculatesCorrectly()
        {
            // Arrange
            var selection = new TimelineSelection();
            var startTime = TimeSpan.FromSeconds(10);
            var endTime = TimeSpan.FromSeconds(20);

            // Act - Forward selection
            selection.StartSelection(startTime);
            selection.UpdateSelection(endTime);

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(10), selection.Duration);

            // Act - Backward selection
            selection.StartSelection(endTime);
            selection.UpdateSelection(startTime);

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(10), selection.Duration);
        }

        [Fact]
        public void NormalizedRange_ReturnsStartBeforeEnd()
        {
            // Arrange
            var selection = new TimelineSelection();
            var time1 = TimeSpan.FromSeconds(10);
            var time2 = TimeSpan.FromSeconds(20);

            // Act - Forward selection
            selection.StartSelection(time1);
            selection.UpdateSelection(time2);

            // Assert
            Assert.Equal(time1, selection.NormalizedStart);
            Assert.Equal(time2, selection.NormalizedEnd);

            // Act - Backward selection
            selection.StartSelection(time2);
            selection.UpdateSelection(time1);

            // Assert
            Assert.Equal(time1, selection.NormalizedStart);
            Assert.Equal(time2, selection.NormalizedEnd);
        }

        [Fact]
        public void ClearSelection_ResetsState()
        {
            // Arrange
            var selection = new TimelineSelection();
            selection.StartSelection(TimeSpan.FromSeconds(10));
            selection.UpdateSelection(TimeSpan.FromSeconds(20));

            // Act
            selection.ClearSelection();

            // Assert
            Assert.False(selection.IsActive);
            Assert.Equal(TimeSpan.Zero, selection.SelectionStart);
            Assert.Equal(TimeSpan.Zero, selection.SelectionEnd);
        }

        [Fact]
        public void IsValid_ReturnsFalseForZeroDuration()
        {
            // Arrange
            var selection = new TimelineSelection();

            // Act - No selection
            var isValidNoSelection = selection.IsValid;

            // Assert
            Assert.False(isValidNoSelection);

            // Act - Zero duration selection
            selection.StartSelection(TimeSpan.FromSeconds(10));
            var isValidZeroDuration = selection.IsValid;

            // Assert
            Assert.False(isValidZeroDuration);

            // Act - Very small selection (less than 10ms)
            selection.UpdateSelection(TimeSpan.FromSeconds(10.005)); // 5ms
            var isValidTooSmall = selection.IsValid;

            // Assert
            Assert.False(isValidTooSmall);

            // Act - Valid selection (more than 10ms)
            selection.UpdateSelection(TimeSpan.FromSeconds(10.015)); // 15ms
            var isValidGoodDuration = selection.IsValid;

            // Assert
            Assert.True(isValidGoodDuration);
        }
    }
}
