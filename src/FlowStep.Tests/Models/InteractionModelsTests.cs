using FlowStep.Models;
using FluentAssertions;
using Xunit;

namespace FlowStep.Tests.Models;

/// <summary>
/// Tests for Interaction Models (InteractionOption, InteractionRequest, InteractionResponse)
/// </summary>
public class InteractionModelsTests
{
    #region InteractionOption Tests

    [Fact]
    public void InteractionOption_Constructor_ShouldSetProperties()
    {
        // Arrange & Act
        var option = new InteractionOption("Label Test", "Value Test", true);

        // Assert
        option.Label.Should().Be("Label Test");
        option.Value.Should().Be("Value Test");
        option.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void InteractionOption_Constructor_ShouldDefaultIsDefaultToFalse()
    {
        // Arrange & Act
        var option = new InteractionOption("Label", "Value");

        // Assert
        option.IsDefault.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Some description")]
    public void InteractionOption_Description_ShouldAllowNullOrValue(string? description)
    {
        // Arrange & Act
        var option = new InteractionOption("Label", "Value") { Description = description };

        // Assert
        option.Description.Should().Be(description);
    }

    #endregion

    #region InteractionRequest Tests

    [Fact]
    public void InteractionRequest_DefaultConstructor_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var request = new InteractionRequest();

        // Assert
        request.Title.Should().Be("Sistema");
        request.Message.Should().BeEmpty();
        request.Type.Should().Be(InteractionType.Notification);
        request.Options.Should().BeNull();
        request.Timeout.Should().BeNull();
        request.AllowCustomInput.Should().BeFalse();
        request.CustomInputPlaceholder.Should().Be("Digite aqui...");
        request.IsCancellable.Should().BeFalse();
        request.MinSelections.Should().Be(0);
        request.MaxSelections.Should().Be(1);
    }

    [Fact]
    public void InteractionRequest_Notify_ShouldCreateNotificationType()
    {
        // Arrange & Act
        var request = InteractionRequest.Notify("Test message");

        // Assert
        request.Type.Should().Be(InteractionType.Notification);
        request.Message.Should().Be("Test message");
    }

    [Fact]
    public void InteractionRequest_Confirm_ShouldCreateConfirmationWithOptions()
    {
        // Arrange & Act
        var request = InteractionRequest.Confirm("Are you sure?");

        // Assert
        request.Type.Should().Be(InteractionType.Confirmation);
        request.Message.Should().Be("Are you sure?");
        request.Options.Should().NotBeNull();
        request.Options!.Count.Should().Be(2);
        
        request.Options[0].Label.Should().Be("Sim");
        request.Options[0].Value.Should().Be("yes");
        request.Options[0].IsDefault.Should().BeTrue();
        
        request.Options[1].Label.Should().Be("Não");
        request.Options[1].Value.Should().Be("no");
        request.Options[1].IsDefault.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InteractionRequest_Choose_ShouldCreateCorrectType(bool allowOther)
    {
        // Arrange
        var options = new List<InteractionOption>
        {
            new("Option 1", "opt1"),
            new("Option 2", "opt2")
        };

        // Act
        var request = InteractionRequest.Choose("Choose one:", options, allowOther);

        // Assert
        if (allowOther)
        {
            request.Type.Should().Be(InteractionType.ChoiceWithText);
            request.AllowCustomInput.Should().BeTrue();
        }
        else
        {
            request.Type.Should().Be(InteractionType.SingleChoice);
            request.AllowCustomInput.Should().BeFalse();
        }
    }

    [Fact]
    public void InteractionRequest_Choose_ShouldSetOptionsCorrectly()
    {
        // Arrange
        var options = new List<InteractionOption>
        {
            new("A", "a"),
            new("B", "b", true), // default
            new("C", "c")
        };

        // Act
        var request = InteractionRequest.Choose("Test", options);

        // Assert
        request.Options.Should().NotBeNull();
        request.Options!.Count.Should().Be(3);
    }

    [Fact]
    public void InteractionRequest_AllInteractionTypes_ShouldBeValid()
    {
        // Arrange & Act - Test all enum values can be assigned
        var types = Enum.GetValues<InteractionType>();

        // Assert
        types.Should().Contain(InteractionType.Notification);
        types.Should().Contain(InteractionType.Confirmation);
        types.Should().Contain(InteractionType.SingleChoice);
        types.Should().Contain(InteractionType.MultiChoice);
        types.Should().Contain(InteractionType.TextInput);
        types.Should().Contain(InteractionType.ChoiceWithText);
    }

    [Theory]
    [InlineData("Test Title", "Test Message", InteractionType.Notification)]
    [InlineData("", "", InteractionType.Confirmation)]
    [InlineData(null, null, InteractionType.SingleChoice)]
    public void InteractionRequest_ShouldAllowVariousTitleAndMessage(string? title, string? message, InteractionType type)
    {
        // Arrange & Act
        var request = new InteractionRequest 
        { 
            Title = title ?? "",
            Message = message ?? "",
            Type = type
        };

        // Assert
        request.Title.Should().Be(title ?? "");
        request.Message.Should().Be(message ?? "");
        request.Type.Should().Be(type);
    }

    [Fact]
    public void InteractionRequest_Timeout_ShouldWorkWithTimeSpan()
    {
        // Arrange & Act
        var timeout = TimeSpan.FromSeconds(30);
        var request = new InteractionRequest { Timeout = timeout };

        // Assert
        request.Timeout.Should().Be(timeout);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 5)]
    [InlineData(3, 10)]
    public void InteractionRequest_MultiChoiceSelections_ShouldSetMinMax(int min, int max)
    {
        // Arrange & Act
        var request = new InteractionRequest 
        { 
            Type = InteractionType.MultiChoice,
            MinSelections = min,
            MaxSelections = max
        };

        // Assert
        request.MinSelections.Should().Be(min);
        request.MaxSelections.Should().Be(max);
    }

    #endregion

    #region InteractionResponse Tests

    [Fact]
    public void InteractionResponse_DefaultConstructor_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var response = new InteractionResponse();

        // Assert
        response.Success.Should().BeFalse();
        response.TimedOut.Should().BeFalse();
        response.Cancelled.Should().BeFalse();
        response.TextValue.Should().BeNull();
        response.SelectedValues.Should().BeNull();
        response.CustomInput.Should().BeNull();
    }

    [Theory]
    [InlineData("test value", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void InteractionResponse_TextValue_ShouldSetCorrectly(string? textValue, bool expectedSuccess)
    {
        // Arrange & Act
        var response = new InteractionResponse { TextValue = textValue };

        // Assert - Note: Success is not auto-calculated in the model itself
        response.TextValue.Should().Be(textValue);
    }

    [Fact]
    public void InteractionResponse_SelectedValues_ShouldAllowMultipleSelections()
    {
        // Arrange & Act
        var response = new InteractionResponse 
        { 
            SelectedValues = new List<string> { "opt1", "opt2", "opt3" }
        };

        // Assert
        response.SelectedValues.Should().NotBeNull();
        response.SelectedValues!.Count.Should().Be(3);
        response.SelectedValues.Should().Contain("opt1");
        response.SelectedValues.Should().Contain("opt2");
        response.SelectedValues.Should().Contain("opt3");
    }

    [Fact]
    public void InteractionResponse_CustomInput_ShouldWorkWithTextValue()
    {
        // Arrange & Act
        var response = new InteractionResponse 
        { 
            CustomInput = "Custom text",
            TextValue = "Custom text"
        };

        // Assert
        response.CustomInput.Should().Be("Custom text");
        response.TextValue.Should().Be("Custom text");
    }

    [Theory]
    [InlineData(true, false, false)]  // Success only
    [InlineData(false, true, false)]  // Timed out only
    [InlineData(false, false, true)]  // Cancelled only
    [InlineData(true, true, false)]   // Success + Timeout (edge case)
    public void InteractionResponse_StatusFlags_ShouldWorkIndependently(bool success, bool timedOut, bool cancelled)
    {
        // Arrange & Act
        var response = new InteractionResponse 
        { 
            Success = success,
            TimedOut = timedOut,
            Cancelled = cancelled
        };

        // Assert
        response.Success.Should().Be(success);
        response.TimedOut.Should().Be(timedOut);
        response.Cancelled.Should().Be(cancelled);
    }

    #endregion
}
