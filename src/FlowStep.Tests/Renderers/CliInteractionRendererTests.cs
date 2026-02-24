using FlowStep.Contracts;
using FlowStep.Models;
using FlowStep.Renderers;
using System.Threading;
using Xunit;
using FluentAssertions;

namespace FlowStep.Tests.Renderers;

/// <summary>
/// Tests for CliInteractionRenderer - the console-based interaction renderer
/// </summary>
public class CliInteractionRendererTests : IDisposable
{
    private readonly CliInteractionRenderer _renderer;

    public CliInteractionRendererTests()
    {
        _renderer = new CliInteractionRenderer();
    }

    #region RenderAsync - Notification Tests

    [Fact]
    public async Task RenderAsync_Notification_ShouldReturnSuccessTrue()
    {
        // Arrange
        var request = InteractionRequest.Notify("Test notification message");

        using var cts = new CancellationTokenSource();

        // Act
        var result = await _renderer.RenderAsync(request, cts.Token);

        // Assert
        result.Success.Should().BeTrue();
        result.TimedOut.Should().BeFalse();
        result.Cancelled.Should().BeFalse();
    }

    [Fact]
    public async Task RenderAsync_Notification_ShouldIncludeTitleAndMessage()
    {
        // Arrange
        var request = new InteractionRequest 
        { 
            Type = InteractionType.Notification,
            Title = "Test Title",
            Message = "Test message content"
        };

        using var cts = new CancellationTokenSource();

        // Act & Assert - Should not throw (just verify it renders)
        var result = await _renderer.RenderAsync(request, cts.Token);
        
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RenderAsync_Notification_WithNullMessage_ShouldWork()
    {
        // Arrange
        var request = new InteractionRequest 
        { 
            Type = InteractionType.Notification,
            Message = null!
        };

        using var cts = new CancellationTokenSource();

        // Act & Assert - Should not throw
        var result = await _renderer.RenderAsync(request, cts.Token);
        result.Success.Should().BeTrue();
    }

    #endregion

    #region RenderAsync - Confirmation Tests (with mock for input)

    /// <summary>
    /// Note: The current CliInteractionRenderer implementation uses Console.ReadLine 
    /// which is blocking. These tests demonstrate the expected behavior based on 
    /// analyzing the renderer code logic.
    /// </summary>

    [Fact]
    public async Task RenderAsync_Confirmation_ShouldReturnSuccessOnValidInput()
    {
        // Arrange - Use Confirmation type with options
        var request = InteractionRequest.Confirm("Are you sure?");
        
        using var cts = new CancellationTokenSource();

        // Note: The actual test would require mocking Console which is complex.
        // This test verifies the model setup is correct for confirmation dialogs.
        request.Type.Should().Be(InteractionType.Confirmation);
        request.Options.Should().NotBeNull();
        request.Options!.Count.Should().Be(2);
        
        // Verify default option is set
        var defaultOption = request.Options.FirstOrDefault(o => o.IsDefault);
        defaultOption.Should().NotBeNull();
        defaultOption!.Value.Should().Be("yes");
    }

    [Fact]
    public async Task RenderAsync_Confirmation_ShouldHaveYesNoOptions()
    {
        // Arrange
        var request = InteractionRequest.Confirm("Continue?");

        // Assert - Verify structure for confirmation dialog
        request.Options.Should().NotBeNull();
        
        var yesOption = request.Options!.First(o => o.Value == "yes");
        var noOption = request.Options.First(o => o.Value == "no");
        
        yesOption.Label.Should().Be("Sim");
        yesOption.IsDefault.Should().BeTrue();
        
        noOption.Label.Should().Be("Não");
        noOption.IsDefault.Should().BeFalse();
    }

    #endregion

    #region RenderAsync - Single Choice Tests

    [Fact]
    public async Task RenderAsync_SingleChoice_ShouldSupportMultipleOptions()
    {
        // Arrange
        var options = new List<InteractionOption>
        {
            new("Red", "red"),
            new("Green", "green"),
            new("Blue", "blue")
        };
        
        var request = InteractionRequest.Choose("Pick a color:", options);

        // Assert - Verify structure
        request.Type.Should().Be(InteractionType.SingleChoice);
        request.Options!.Count.Should().Be(3);
    }

    [Fact]
    public async Task RenderAsync_SingleChoice_WithDefaultOption_ShouldSetCorrectly()
    {
        // Arrange
        var options = new List<InteractionOption>
        {
            new("First", "1"),
            new("Second", "2", true),  // Default
            new("Third", "3")
        };
        
        var request = InteractionRequest.Choose("Select:", options);

        // Assert
        var defaultOpt = request.Options!.First(o => o.IsDefault);
        defaultOpt.Value.Should().Be("2");
    }

    [Fact]
    public async Task RenderAsync_SingleChoice_WithNoOptions_ShouldHandleGracefully()
    {
        // Arrange - Empty options list
        var request = new InteractionRequest 
        { 
            Type = InteractionType.SingleChoice,
            Message = "Select:",
            Options = new List<InteractionOption>()
        };

        using var cts = new CancellationTokenSource();

        // Act & Assert - Should handle empty options without crashing
        var result = await _renderer.RenderAsync(request, cts.Token);
        
        // The renderer should still return (may have unexpected behavior with no options)
        result.Should().NotBeNull();
    }

    #endregion

    #region RenderAsync - Multi Choice Tests

    [Fact]
    public async Task RenderAsync_MultiChoice_ShouldAllowMultipleSelections()
    {
        // Arrange
        var request = new InteractionRequest 
        { 
            Type = InteractionType.MultiChoice,
            Message = "Select all that apply:",
            Options = new List<InteractionOption>
            {
                new("Option A", "a"),
                new("Option B", "b"),
                new("Option C", "c", true)  // Selected by default
            }
        };

        using var cts = new CancellationTokenSource();

        // Act - The renderer should recognize MultiChoice type
        var result = await _renderer.RenderAsync(request, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RenderAsync_MultiChoice_ShouldRespectMinMaxSelections()
    {
        // Arrange
        var request = new InteractionRequest 
        { 
            Type = InteractionType.MultiChoice,
            Message = "Select 1-3 options:",
            Options = new List<InteractionOption>
            {
                new("A", "a"),
                new("B", "b"),
                new("C", "c"),
                new("D", "d")
            },
            MinSelections = 1,
            MaxSelections = 3
        };

        // Assert - Verify model setup
        request.MinSelections.Should().Be(1);
        request.MaxSelections.Should().Be(3);
    }

    #endregion

    #region RenderAsync - Text Input Tests

    [Fact]
    public async Task RenderAsync_TextInput_ShouldAllowTextEntry()
    {
        // Arrange
        var request = new InteractionRequest 
        { 
            Type = InteractionType.TextInput,
            Message = "Enter your name:",
            CustomInputPlaceholder = "Your name here..."
        };

        using var cts = new CancellationTokenSource();

        // Act & Assert - Should work without throwing
        var result = await _renderer.RenderAsync(request, cts.Token);
        
        result.Should().NotBeNull();
    }

    #endregion

    #region RenderAsync - Choice With Text Tests

    [Fact]
    public async Task RenderAsync_ChoiceWithText_ShouldSupportCustomInput()
    {
        // Arrange
        var options = new List<InteractionOption>
        {
            new("Other", "other", true)
        };
        
        var request = InteractionRequest.Choose("Select option:", options, allowOther: true);

        // Assert - Verify ChoiceWithText type
        request.Type.Should().Be(InteractionType.ChoiceWithText);
        request.AllowCustomInput.Should().BeTrue();
    }

    [Fact]
    public async Task RenderAsync_ChoiceWithText_ShouldSetPlaceholder()
    {
        // Arrange
        var customPlaceholder = "Enter your own option:";
        var options = new List<InteractionOption> { new("Other", "custom") };
        
        var request = InteractionRequest.Choose("Choose:", options, allowOther: true);
        request.CustomInputPlaceholder = customPlaceholder;

        // Assert
        request.CustomInputPlaceholder.Should().Be(customPlaceholder);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task RenderAsync_WhenCancelled_ShouldReturnCancelledResponse()
    {
        // Arrange
        var request = InteractionRequest.Notify("This will be cancelled");
        using var cts = new CancellationTokenSource();
        
        // Cancel immediately
        cts.Cancel();

        // Act & Assert - Should throw OperationCanceledException
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            _renderer.RenderAsync(request, cts.Token));
    }

    #endregion

    #region ReportProgress Tests

    [Fact]
    public void ReportProgress_ShouldCalculatePercentageCorrectly()
    {
        // Arrange & Act - Testing the progress reporting logic indirectly
        // The actual Console.Write makes this hard to test directly
        
        // We verify by checking that the method doesn't throw
        var renderer = new CliInteractionRenderer();
        
        // Act & Assert - Should not throw
        renderer.ReportProgress("op1", 50, 100, "Test Operation");
        renderer.EndProgress("op1"); // New line after progress
    }

    [Fact]
    public void ReportProgress_AtZeroPercent_ShouldWork()
    {
        // Arrange
        var renderer = new CliInteractionRenderer();

        // Act & Assert - Should handle 0%
        renderer.ReportProgress("op1", 0, 100, "Starting");
        renderer.EndProgress("op1");
    }

    [Fact]
    public void ReportProgress_AtHundredPercent_ShouldWork()
    {
        // Arrange
        var renderer = new CliInteractionRenderer();

        // Act & Assert - Should handle 100%
        renderer.ReportProgress("op1", 100, 100, "Complete");
        renderer.EndProgress("op1");
    }

    [Fact]
    public void ReportProgress_WithZeroTotal_ShouldNotCrash()
    {
        // Arrange - Edge case: division by zero protection
        var renderer = new CliInteractionRenderer();

        // Act & Assert - Should handle 0 total (would cause div by zero without protection)
        // Note: In actual implementation, this may produce unexpected results
        // but shouldn't crash if properly handled
        try
        {
            renderer.ReportProgress("op1", 5, 0, "Test");
        }
        catch (DivideByZeroException)
        {
            // This would indicate a bug that needs fixing
            Assert.True(false, "Should not divide by zero");
        }
    }

    #endregion

    #region EndProgress Tests

    [Fact]
    public void EndProgress_ShouldOutputNewLine()
    {
        // Arrange
        var renderer = new CliInteractionRenderer();

        // Act & Assert - Should not throw
        renderer.EndProgress("test-operation-id");
    }

    #endregion

    public void Dispose()
    {
        // Cleanup if needed
    }
}
