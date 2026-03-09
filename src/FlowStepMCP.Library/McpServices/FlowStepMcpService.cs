using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowStep.Models;
using FlowStep.Services;
using FlowStep.Contracts;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FlowStep.McpServices
{
    /// <summary>
    /// MCP service for user interactions via FlowStep.
    /// </summary>
    [McpServerToolType]
    public class FlowStepMcpService
    {
        private readonly IFlowStepService _flowStepService;
        private readonly ILogger<FlowStepMcpService> _logger;

        public FlowStepMcpService(IFlowStepService flowStepService, ILogger<FlowStepMcpService> logger)
        {
            _flowStepService = flowStepService;
            _logger = logger;
        }

        /// <summary>
        /// Displays a simple notification to the user, optionally waiting for confirmation.
        /// </summary>
        /// <param name="message">Message to be displayed</param>
        /// <param name="title">Notification title (optional; default: "System")</param>
        /// <param name="waitConfirmation">
        /// If true, waits for user confirmation. Useful for critical actions.
        /// If false (default), does not block task progress—ideal for informative notifications.
        /// </param>
        /// <returns>Status of the operation</returns>
        [McpServerTool]
        [Description("Displays a simple notification to the user with a title and message. Can optionally wait for user confirmation or be non-blocking (default).")]
        public async Task<string> NotifyUserAsync(
            [Description("Message to be displayed to the user")]
            string message,
            [Description("Notification title (optional; default: 'System')")]
            string title,
            [Description("If true, waits for user confirmation. Default: false (non-blocking notification).")]
            bool waitConfirmation = false)
        {
            _logger.LogInformation(
                "Notification requested: {Title} - {Message} | Wait Confirmation: {Wait}",
                title ?? "System",
                message,
                waitConfirmation);

            var request = new InteractionRequest
            {
                Title = title ?? "System",
                Message = message,
                Type = waitConfirmation ? InteractionType.Confirmation : InteractionType.Notification
            };

            if (!waitConfirmation)
            {
                Task.Run(() => _flowStepService.InteractAsync(request));
                return $"Notification displayed. User did not confirm.";
            }
            else
            {
                var response = await _flowStepService.InteractAsync(request);

                if (response.Success)
                {
                    return $"Notification {(waitConfirmation ? "confirmed" : "displayed")} successfully: {message}";
                }
            }

            return $"Failed to display notification.";
        }


        /// <summary>
        /// Requests user confirmation (Yes/No).
        /// </summary>
        /// <param name="message">Confirmation message</param>
        /// <param name="title">Confirmation title (optional)</param>
        /// <param name="isCancellable">Whether the operation can be cancelled by the user (optional; default: true)</param>
        /// <returns>User response: "yes", "no", or "cancelled"</returns>
        [McpServerTool]
        [Description("Requests user confirmation with a message. Returns 'yes' if confirmed, 'no' if rejected, or 'cancelled' if cancelled. Useful for critical actions or important decisions.")]
        public async Task<string> ConfirmAsync(
            [Description("Confirmation message to the user")]
            string message,
            [Description("Confirmation title (optional)")]
            string title,
            [Description("Indicates whether the operation can be cancelled by the user (optional; default: true)")]
            bool isCancellable = true)
        {
            _logger.LogInformation("Confirmation requested: {Title} - {Message}", title ?? "System", message);

            var request = new InteractionRequest
            {
                Title = title ?? "System",
                Message = message,
                Type = InteractionType.Confirmation,
                IsCancellable = isCancellable
            };

            var response = await _flowStepService.InteractAsync(request);

            if (response.Cancelled)
            {
                return "cancelled";
            }

            if (response.SelectedValues != null && response.SelectedValues.Contains("yes"))
            {
                return "yes";
            }

            return "no";
        }

        /// <summary>
        /// Allows the user to choose one option among several.
        /// </summary>
        /// <param name="message">Choice message</param>
        /// <param name="options">List of available options</param>
        /// <param name="title">Title of the choice (optional)</param>
        /// <param name="allowCustomInput">Whether to allow a custom input option (optional; default: false)</param>
        /// <returns>Value of the selected option or "custom:{value}" if a custom value is provided</returns>
        [McpServerTool]
        [Description("Allows the user to choose one option among several available ones. Returns the value of the selected option. Useful for simple user selections.")]
        public async Task<string> ChooseOptionAsync(
            [Description("Message describing the available options")]
            string message,
            [Description("List of options available for selection. Each option has Label, Value, and may have IsDefault")]
            List<InteractionOption> options,
            [Description("Title of the choice (optional)")]
            string title,
            [Description("Whether to allow the user to type a custom option (optional; default: false)")]
            bool allowCustomInput = false)
        {
            _logger.LogInformation("Choice requested: {Title} - {Message}", title ?? "System", message);

            var request = new InteractionRequest
            {
                Title = title ?? "System",
                Message = message,
                Type = allowCustomInput ? InteractionType.ChoiceWithText : InteractionType.SingleChoice,
                Options = options,
                AllowCustomInput = allowCustomInput
            };

            var response = await _flowStepService.InteractAsync(request);

            if (response.Cancelled)
            {
                return "cancelled";
            }

            if (response.SelectedValues != null && response.SelectedValues.Count > 0)
            {
                return response.SelectedValues[0];
            }

            if (response.CustomInput != null)
            {
                return $"custom:{response.CustomInput}";
            }

            return "cancelled";
        }

        /// <summary>
        /// Allows the user to select multiple options.
        /// </summary>
        /// <param name="message">Multiple-selection message</param>
        /// <param name="options">List of available options</param>
        /// <param name="minSelections">Minimum required selections (optional; default: 0)</param>
        /// <param name="maxSelections">Maximum allowed selections (optional; default: 1)</param>
        /// <param name="title">Title of the selection (optional)</param>
        /// <returns>List of values of selected options</returns>
        [McpServerTool]
        [Description("Allows the user to select multiple options among several available ones. Returns a list containing the values of selected options. Useful for multi-select scenarios such as filters or multiple items.")]
        public async Task<List<string>> ChooseMultipleOptionsAsync(
            [Description("Title of the selection (optional)")]
            string title,
            [Description("Message describing the available options")]
            string message,
            [Description("List of options available for selection. Each option has Label, Value, and may have IsDefault")]
            List<InteractionOption> options,
            [Description("Minimum number of required selections (optional; default: 0)")]
            int minSelections = 0,
            [Description("Maximum number of allowed selections (optional; default: 1)")]
            int maxSelections = 1)
        {
            _logger.LogInformation("Multiple selection requested: {Title} - {Message}", title ?? "System", message);

            var request = new InteractionRequest
            {
                Title = title ?? "System",
                Message = message,
                Type = InteractionType.MultiChoice,
                Options = options,
                MinSelections = minSelections,
                MaxSelections = maxSelections
            };

            var response = await _flowStepService.InteractAsync(request);

            if (response.Cancelled)
            {
                return new List<string>();
            }

            if (response.SelectedValues != null && response.SelectedValues.Count > 0)
            {
                return response.SelectedValues;
            }

            if (response.CustomInput != null)
            {
                return new List<string> { $"custom:{response.CustomInput}" };
            }

            return new List<string>();
        }

        /// <summary>
        /// Requests free-form text input from the user.
        /// </summary>
        /// <param name="message">Instruction or prompt for the user</param>
        /// <param name="title">Title of the text field (optional)</param>
        /// <param name="placeholder">Placeholder text (optional; default: "Type here...")</param>
        /// <returns>The text entered by the user</returns>

        [McpServerTool]
        [Description("Requests that the user type free-form text. Returns the text entered by the user.")]
        public async Task<string> AskUserForTextAsync(
            [Description("Instruction or message to the user")]
    string message,
            [Description("Title of the text field (optional)")]
    string? title,
            [Description("Placeholder text shown in the input field (optional; default: 'Type here...')")]
    string placeholder)
        {
            _logger.LogInformation("Text input requested: {Title} - {Message}", title ?? "System", message);

            var request = new InteractionRequest
            {
                Title = title ?? "System",
                Message = message,
                Type = InteractionType.TextInput,
                CustomInputPlaceholder = placeholder ?? "Type here..."
            };

            var response = await _flowStepService.InteractAsync(request);

            // CORREÇÃO: Verificar TextValue primeiro, depois SelectedValues como fallback
            if (response.Success)
            {
                if (!string.IsNullOrEmpty(response.TextValue))
                    return response.TextValue;

                if (response.SelectedValues != null && response.SelectedValues.Count > 0)
                    return response.SelectedValues[0];
            }

            return "";
        }

        /// <summary>
        /// Requests the user to select an option and optionally type a custom text.
        /// </summary>
        /// <param name="message">Instruction message</param>
        /// <param name="options">List of available options</param>
        /// <param name="title">Title of the interaction (optional)</param>
        /// <param name="placeholder">Placeholder for the custom text field (optional)</param>
        /// <returns>Selected option value or custom text prefixed with "custom:"</returns>
        [McpServerTool]
        [Description("Allows the user to choose one option and optionally type a custom text. Returns either the selected option or the custom text. Useful when users may select from predefined options or provide a custom response.")]
        public async Task<string> ChooseWithCustomTextAsync(
            [Description("Instruction message for the user")]
            string message,
            [Description("List of options available for selection. Each option has Label, Value, and may have IsDefault")]
            List<InteractionOption> options,
            [Description("Title of the interaction (optional)")]
            string title,
            [Description("Placeholder text for the custom text input field (optional)")]
            string placeholder)
        {
            _logger.LogInformation("Choice with custom text requested: {Title} - {Message}", title ?? "System", message);

            var request = new InteractionRequest
            {
                Title = title ?? "System",
                Message = message,
                Type = InteractionType.ChoiceWithText,
                Options = options,
                AllowCustomInput = true,
                CustomInputPlaceholder = placeholder ?? "Type here..."
            };

            var response = await _flowStepService.InteractAsync(request);

            if (response.Cancelled)
            {
                return "cancelled";
            }

            if (response.CustomInput != null)
            {
                return $"custom:{response.CustomInput}";
            }

            if (response.SelectedValues != null && response.SelectedValues.Count > 0)
            {
                return response.SelectedValues[0];
            }

            return "cancelled";
        }

        /// <summary>
        /// Displays a notification with progress bar.
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="total">Total number of items to process</param>
        /// <param name="status">Current status or progress message</param>
        /// <returns>Status of the operation</returns>
        [McpServerTool]
        [Description("Displays a notification indicating the progress of an operation. Useful for long-running tasks or batch processing.")]
        public async Task<string> ShowProgressAsync(
            [Description("Descriptive name of the ongoing operation")]
            string operationName,
            [Description("Total number of items to process")]
            int total,
            [Description("Current status or progress message")]
            string status)
        {
            _logger.LogInformation("Progress requested: {Operation} - {Status}", operationName, status);

            //TODO: IMPLEMENT HERE. 
            return $"Operation '{operationName}' completed successfully.";
        }
    }
}
