# FlowStep - MCP Server for User Interactions

![GitHub Version](https://img.shields.io/badge/version-0.1.0--alpha-red)
![GitHub Status](https://img.shields.io/badge/status-development-yellow)

# 🚧 **Under Development**


A complete Model Context Protocol (MCP) server designed to facilitate seamless interaction between Large Language Models (LLMs) and end-users. It provides a robust set of tools for notifications, confirmations, selections, and text inputs, supporting GUI rendering mode.

<video src="assets/StepFlowMCP.mp4" controls style="width: 80%; max-width: 800px;  margin: 0 auto;"></video>


## 🎯 Overview

FlowStep acts as an abstraction layer for user interactions. It exposes standard MCP tools that LLMs can invoke to interact with the user based on the application's configuration (Console or GUI).

**Key Capabilities:**
*   **Notifications**: Display non-blocking or blocking informational messages.
*   **Confirmations**: Request explicit Yes/No or Cancel confirmation from the user.
*   **Single & Multi-Selection**: Provide dropdowns or lists for choosing one or multiple options.
*   **Text Input**: Collect free-form text from the user.
*   **Custom Input**: Allow selection from a predefined list *or* custom text entry.
*   **Progress Reporting**: Visual feedback for long-running operations.
*   **GUI Support**: Integrated Avalonia UI rendering for modern desktop applications.

## 📦 Project Structure

The library is organized into logical layers:

```
FlowStep.MCP.Library/
├── Models/
│   └── InteractionModels.cs           # Data models (InteractionRequest, InteractionResponse, InteractionOption)
├── Contracts/
│   ├── IFlowStepService.cs            # Core service interface
│   └── IInteractionRenderer.cs        # Renderer interface (Contracts for UI implementation)
├── Services/
│   └── FlowStepService.cs             # Business logic and orchestration
├── McpServices/
│   └── FlowStepMcpService.cs          # Implementation of MCP Server Tools
├── Renderers/
│   ├── CliInteractionRenderer.cs      # Console-based implementation
│   ├── AvaloniaUI/
│   │   ├── AvaloniaUIRenderer.cs      # Main Avalonia GUI renderer
│   │   ├── Themes/
│   │   │   └── ThemeColors.cs         # Dark mode color definitions
│   │   ├── Header/
│   │   │   └── HeaderContentFactory.cs
│   │   ├── Footer/
│   │   │   ├── StandardFooterFactory.cs
│   │   │   └── NotificationFooterFactory.cs
│   │   ├── Inputs/
│   │   │   ├── SingleChoiceInputFactory.cs
│   │   │   ├── MultiChoiceInputFactory.cs
│   │   │   ├── TextInputFactory.cs
│   │   │   └── ChoiceWithTextInputFactory.cs
│   │   ├── Factories/
│   │   │   ├── ConfirmationButtonsFactory.cs
│   │   │   ├── SimpleConfirmationContentFactory.cs
│   │   │   └── ResponseBuilder.cs
│   │   └── Styles/
│   │       └── DarkThemeStyles.cs     # XAML-like styling logic
├── Extensions/
│   └── FlowStepServiceExtension.cs    # DI Registration helper
└── FlowStep.MCP.Library.csproj
```


## 🌐 Configuration in MCP Clients

To integrate FlowStep with your favorite AI editor or client (e.g., **Cursor, Windsurf, Claude Desktop**, or **Cline**), you need to add the server configuration to your client's settings.

### 1. HTTP Transport (Recommended)
Use this configuration if you are running the server via HTTP (e.g., using `dotnet run` or a hosted server).

```json
{
  "mcpServers": {
    "FlowStep.MCP": {
      "url": "http://localhost:59170"
    }
  }
}
```

### 2. STDIO Transport (Local Execution)
Use this configuration to launch the server directly from the project folder.

```json
{
  "mcpServers": {
    "FlowStep.MCP": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "src/FlowStep.MCP/FlowStep.MCP.csproj"
      ]
    }
  }
}
```

***

### Note on the previous C# code
The Dependency Injection code provided in the previous section (`services.AddFlowStep(...)`) is **internal** to your application and is not used for configuring the external MCP client. It is used when you are *developing* the FlowStep server itself.

## 🛠️ MCP Tools Reference

All tools are exposed via the `FlowStepMcpService` and automatically registered with the MCP server. The descriptions below are extracted from the source code `[Description]` attributes.

### 1. NotifyUserAsync
Displays a simple notification to the user with a title and message. Can optionally wait for user confirmation or be non-blocking (default).

*   **Parameters**:
    *   `message` (string): Message to be displayed to the user.
    *   `title` (string): Notification title (optional; default: 'System').
    *   `waitConfirmation` (bool): If true, waits for user confirmation. Default: false (non-blocking notification).
*   **Returns**: Status of the operation.

### 2. ConfirmAsync
Requests user confirmation with a message. Returns 'yes' if confirmed, 'no' if rejected, or 'cancelled' if cancelled.

*   **Parameters**:
    *   `message` (string): Confirmation message to the user.
    *   `title` (string): Confirmation title (optional).
    *   `isCancellable` (bool): Indicates whether the operation can be cancelled by the user (optional; default: true).
*   **Returns**: "yes", "no", or "cancelled".

### 3. ChooseOptionAsync
Allows the user to choose one option among several available ones. Returns the value of the selected option.

*   **Parameters**:
    *   `message` (string): Message describing the available options.
    *   `options` (List<InteractionOption>): List of options available for selection.
    *   `title` (string): Title of the choice (optional).
    *   `allowCustomInput` (bool): Whether to allow a custom input option (optional; default: false).
*   **Returns**: Value of the selected option or "custom:{value}" if custom input is provided.

### 4. ChooseMultipleOptionsAsync
Allows the user to select multiple options among several available ones. Returns a list containing the values of selected options.

*   **Parameters**:
    *   `title` (string): Title of the selection (optional).
    *   `message` (string): Message describing the available options.
    *   `options` (List<InteractionOption>): List of options available for selection.
    *   `minSelections` (int): Minimum number of required selections (optional; default: 0).
    *   `maxSelections` (int): Maximum number of allowed selections (optional; default: 1).
*   **Returns**: List of values of selected options.

### 5. AskUserForTextAsync
Requests that the user type free-form text. Returns the text entered by the user.

*   **Parameters**:
    *   `message` (string): Instruction or message to the user.
    *   `title` (string): Title of the text field (optional).
    *   `placeholder` (string): Placeholder text shown in the input field (optional; default: 'Type here...').
*   **Returns**: The text entered by the user.

### 6. ChooseWithCustomTextAsync
Allows the user to choose one option and optionally type a custom text.

*   **Parameters**:
    *   `message` (string): Instruction message for the user.
    *   `options` (List<InteractionOption>): List of options available for selection.
    *   `title` (string): Title of the interaction (optional).
    *   `placeholder` (string): Placeholder text for the custom text input field (optional).
*   **Returns**: Selected option value or custom text prefixed with "custom:".

### 7. ShowProgressAsync
Displays a notification indicating the progress of an operation. Useful for long-running tasks or batch processing.

*   **Parameters**:
    *   `operationName` (string): Descriptive name of the ongoing operation.
    *   `total` (int): Total number of items to process.
    *   `status` (string): Current status or progress message.
*   **Returns**: Status of the operation.



## 🎨 Interaction Types

The library handles six distinct interaction types defined in `InteractionType`:

1.  **Notification**: Simple display (OK).
2.  **Confirmation**: Sim/Não (Yes/No).
3.  **SingleChoice**: ComboBox / Radio (Select 1).
4.  **MultiChoice**: Checkboxes (Select N).
5.  **TextInput**: Text input only.
6.  **ChoiceWithText**: Options + Custom Text field.

## 🏗️ Architecture

*   **Service Layer**: `FlowStepService` handles the orchestration and timeout management.
*   **Renderer Layer**: `IInteractionRenderer` defines the contract. Implementations include `CliInteractionRenderer` and `AvaloniaUIRenderer`.
*   **MCP Layer**: `FlowStepMcpService` wraps the logic in tools that conform to the Model Context Protocol, allowing LLMs to invoke them transparently.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

<p align="center">Made with ❤️</p>