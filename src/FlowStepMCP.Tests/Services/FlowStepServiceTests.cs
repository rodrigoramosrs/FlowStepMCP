using FlowStep.Contracts;
using FlowStep.Models;
using FlowStep.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using System.Threading;

namespace FlowStep.Tests.Services;

/// <summary>
/// Tests for FlowStepService - the main service orchestrating interactions
/// </summary>
public class FlowStepServiceTests
{
    private readonly Mock<IInteractionRenderer> _mockRenderer;
    private readonly Mock<ILogger<FlowStepService>> _mockLogger;
    private readonly FlowStepService _service;

    public FlowStepServiceTests()
    {
        _mockRenderer = new Mock<IInteractionRenderer>();
        _mockLogger = new Mock<ILogger<FlowStepService>>();
        _service = new FlowStepService(_mockRenderer.Object, _mockLogger.Object);
    }

    #region InteractAsync Tests

    [Fact]
    public async Task InteractAsync_ShouldCallRenderer()
    {
        // Arrange
        var request = InteractionRequest.Notify("Test message");
        var expectedResponse = new InteractionResponse { Success = true };

        _mockRenderer
            .Setup(r => r.RenderAsync(It.IsAny<InteractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.InteractAsync(request);

        // Assert
        _mockRenderer.Verify(
            r => r.RenderAsync(It.Is<InteractionRequest>(req => req.Message == "Test message"), It.IsAny<CancellationToken>()),
            Times.Once);

        result.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task InteractAsync_ShouldPassCorrectCancellationToken()
    {
        // Arrange
        var request = InteractionRequest.Notify("Test");
        using var cts = new CancellationTokenSource();
        var expectedResponse = new InteractionResponse { Success = true };

        _mockRenderer
            .Setup(r => r.RenderAsync(It.IsAny<InteractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        await _service.InteractAsync(request, cts.Token);

        // Assert - Verify the token was passed through
        _mockRenderer.Verify(
            r => r.RenderAsync(It.IsAny<InteractionRequest>(), It.Is<CancellationToken>(ct => ct == cts.Token)),
            Times.Once);
    }

    [Fact]
    public async Task InteractAsync_WhenTimeoutSet_ShouldCreateLinkedCancellation()
    {
        // Arrange
        var request = InteractionRequest.Notify("Test with timeout");
        request.Timeout = TimeSpan.FromMilliseconds(100);

        _mockRenderer
            .Setup(r => r.RenderAsync(It.IsAny<InteractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InteractionResponse { Success = true });

        // Act & Assert - Should not throw even with timeout
        var result = await _service.InteractAsync(request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task InteractAsync_WhenRendererThrowsOperationCanceledException_ShouldReturnCancelledResponse()
    {
        // Arrange
        var request = InteractionRequest.Notify("Test");
        using var userCts = new CancellationTokenSource();

        _mockRenderer
            .Setup(r => r.RenderAsync(It.IsAny<InteractionRequest>(), It.IsAny<CancellationToken>()))
            .Throws(new OperationCanceledException());

        // Act
        var result = await _service.InteractAsync(request, userCts.Token);

        // Assert
        result.Cancelled.Should().BeTrue();
        result.TimedOut.Should().BeFalse(); // User token was not cancelled, so it's a renderer internal cancel
    }

    [Fact]
    public async Task InteractAsync_WhenUserTokenCancelled_ShouldReturnTimedOutAsFalse()
    {
        // Arrange
        var request = InteractionRequest.Notify("Test");
        using var userCts = new CancellationTokenSource();
        userCts.Cancel(); // Cancel the user token before calling

        _mockRenderer
            .Setup(r => r.RenderAsync(It.IsAny<InteractionRequest>(), It.IsAny<CancellationToken>()))
            .Throws(new OperationCanceledException());

        // Act
        var result = await _service.InteractAsync(request, userCts.Token);

        // Assert
        result.Cancelled.Should().BeTrue();
        result.TimedOut.Should().BeFalse();
    }

    [Fact]
    public async Task InteractAsync_AllInteractionTypes_ShouldPassThroughCorrectly()
    {
        // Arrange - Test each interaction type
        var types = new[]
        {
            InteractionType.Notification,
            InteractionType.Confirmation,
            InteractionType.SingleChoice,
            InteractionType.MultiChoice,
            InteractionType.TextInput,
            InteractionType.ChoiceWithText
        };

        foreach (var type in types)
        {
            var request = new InteractionRequest { Type = type, Message = $"Test {type}" };

            _mockRenderer
                .Setup(r => r.RenderAsync(It.IsAny<InteractionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InteractionResponse { Success = true });

            // Act
            var result = await _service.InteractAsync(request);

            // Assert
            result.Success.Should().BeTrue($"Failed for type: {type}");
        }
    }

    [Fact]
    public async Task InteractAsync_WithOptions_ShouldPassToRenderer()
    {
        // Arrange
        var options = new List<InteractionOption>
        {
            new("Option A", "a", true),
            new("Option B", "b"),
            new("Option C", "c")
        };

        var request = InteractionRequest.Choose("Select one:", options);

        _mockRenderer
            .Setup(r => r.RenderAsync(It.IsAny<InteractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InteractionResponse { Success = true, SelectedValues = new List<string> { "a" } });

        // Act
        await _service.InteractAsync(request);
        _mockRenderer.Verify(
            r => r.RenderAsync(
                It.Is<InteractionRequest>(req => req.Options != null && req.Options.Count == 3),
                It.IsAny<CancellationToken>()),
            Times.Once);


    }

    [Fact]
    public async Task InteractAsync_WhenTimeoutExpires_ShouldReturnTimedOutResponse()
    {
        // Arrange - This tests the logic path where renderer returns TimedOut = true
        var request = InteractionRequest.Notify("Test timeout");
        request.Timeout = TimeSpan.FromMilliseconds(10);

        _mockRenderer
            .Setup(r => r.RenderAsync(It.IsAny<InteractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InteractionResponse { Success = false, TimedOut = true });

        // Act
        var result = await _service.InteractAsync(request);

        // Assert
        result.TimedOut.Should().BeTrue();
    }

    [Fact]
    public async Task InteractAsync_MultipleConcurrentCalls_ShouldHandleEach()
    {
        // Arrange
        var callCount = 0;
        _mockRenderer
            .Setup(r => r.RenderAsync(It.IsAny<InteractionRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (InteractionRequest req, CancellationToken ct) =>
            {
                callCount++;
                await Task.Delay(10); // Simulate some work
                return new InteractionResponse { Success = true };
            });

        var request1 = InteractionRequest.Notify("Test 1");
        var request2 = InteractionRequest.Notify("Test 2");
        var request3 = InteractionRequest.Notify("Test 3");

        // Act - Run concurrently
        await Task.WhenAll(
            _service.InteractAsync(request1),
            _service.InteractAsync(request2),
            _service.InteractAsync(request3)
        );

        // Assert
        callCount.Should().Be(3);
    }

    #endregion

    #region CreateProgress Tests

    [Fact]
    public void CreateProgress_ShouldReturnValidProgressReporter()
    {
        // Arrange & Act
        var progress = _service.CreateProgress("TestOperation", 100);

        // Assert
        progress.Should().NotBeNull();
    }

    [Fact]
    public void CreateProgress_WhenReporting_ShouldCallRendererReportProgress()
    {
        // Arrange
        string? capturedOperationId = null;
        int capturedCurrent = 0, capturedTotal = 0;
        string? capturedStatus = null;

        _mockRenderer.Setup(r => r.ReportProgress(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>()))
            .Callback<string, int, int, string>((opId, curr, tot, status) =>
            {
                capturedOperationId = opId;
                capturedCurrent = curr;
                capturedTotal = tot;
                capturedStatus = status;
            });

        var progress = _service.CreateProgress("My Operation", 50);

        // Act
        ((IProgress<(int, int, string)>)progress).Report((25, 50, "Processing"));

        // Assert
        capturedOperationId.Should().NotBeNullOrEmpty();
        capturedCurrent.Should().Be(25);
        capturedTotal.Should().Be(50);
        capturedStatus.Should().Be("Processing");
    }

    [Fact]
    public void CreateProgress_MultipleReports_ShouldCallRendererMultipleTimes()
    {
        // Arrange
        var callCount = 0;
        _mockRenderer.Setup(r => r.ReportProgress(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>()))
            .Callback(() => callCount++);

        var progress = _service.CreateProgress("Test", 100);
        var reporter = (IProgress<(int, int, string)>)progress;

        // Act
        for (int i = 0; i <= 10; i++)
        {
            reporter.Report((i, 10, $"Step {i}"));
        }

        // Assert
        callCount.Should().Be(11);
    }

    #endregion
}
