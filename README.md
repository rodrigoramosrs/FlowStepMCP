# FlowStep - Servidor MCP para Interações com Usuário

Um servidor MCP (Model Context Protocol) completo que expõe métodos para interações com usuários via LLMs, com suporte a CLI, GUI e timeout configurável.

## 🎯 Visão Geral

O FlowStep é um servidor MCP que fornece ferramentas para:
- Exibir notificações
- Solicitar confirmações (Sim/Não)
- Permitir escolha de opções
- Seleção múltipla
- Entrada de texto livre
- Progresso visual
- Campo personalizado expansível

## 📦 Estrutura do Projeto

```
FlowStep/
├── Models/
│   └── InteractionModels.cs           # Tipos e modelos de dados
├── Contracts/
│   ├── IInteractionRenderer.cs        # Interface do renderer
│   └── IFlowStepService.cs            # Interface principal do serviço
├── Services/
│   └── FlowStepService.cs             # Lógica de negócio central
├── Renderers/
│   ├── CliInteractionRenderer.cs      # Implementação para Console
│   └── GuiInteractionBridge.cs        # Bridge para GUI (WPF/Blazor)
├── McpServices/
│   └── FlowStepMcpService.cs          # Serviço MCP exposto para LLMs
├── FlowStep.cs                         # Classe McpBuilder para DI
├── Program.cs                         # Exemplos de uso CLI
├── McpServerProgram.cs                # Programa para servidor MCP HTTP
├── FlowStep.csproj                    # Arquivo do projeto
└── README.md                          # Documentação
```

## 🚀 Quick Start

### 1. Compilar o Servidor MCP

```bash
cd FlowStep
dotnet build
```

### 2. Executar o Servidor MCP HTTP

```bash
dotnet run --project McpServerProgram.cs
```

O servidor estará disponível em:
- **HTTP Transport**: `http://localhost:5000`
- **STDIO Transport**: Via pipe para conexões stdio

## 🛠️ Ferramentas MCP Expostas

Todas as ferramentas são automaticamente descobertas pelo protocolo MCP e podem ser consumidas por LLMs.

### 1. `NotifyUserAsync`

Exibe uma notificação simples para o usuário.

**Parâmetros:**
- `message` (string): Mensagem a ser exibida
- `title` (string, opcional): Título da notificação

**Retorna:** Status da operação

**Exemplo de uso:**
```csharp
await NotifyUserAsync("Processamento concluído com sucesso!", "Atualização");
```

### 2. `ConfirmAsync`

Solicita confirmação do usuário (Sim/Não).

**Parâmetros:**
- `message` (string): Mensagem de confirmação
- `title` (string, opcional): Título da confirmação
- `isCancellable` (bool, opcional): Se pode ser cancelado

**Retorna:** "yes", "no" ou "cancelled"

**Exemplo de uso:**
```csharp
var response = await ConfirmAsync("Deseja salvar as alterações?");
// response pode ser "yes", "no" ou "cancelled"
```

### 3. `ChooseOptionAsync`

Permite que o usuário escolha uma opção entre várias.

**Parâmetros:**
- `message` (string): Mensagem de escolha
- `options` (List<InteractionOption>): Lista de opções
- `title` (string, opcional): Título
- `allowCustomInput` (bool, opcional): Permite opção personalizada

**Retorna:** Valor da opção selecionada

**Exemplo de uso:**
```csharp
var options = new List<InteractionOption>
{
    new("Criar novo arquivo", "create"),
    new("Editar existente", "edit"),
    new("Cancelar", "cancel")
};

var result = await ChooseOptionAsync("O que deseja fazer?", options);
```

### 4. `ChooseMultipleOptionsAsync`

Permite seleção múltipla de opções.

**Parâmetros:**
- `message` (string): Mensagem de seleção
- `options` (List<InteractionOption>): Lista de opções
- `minSelections` (int, opcional): Mínimo de seleções
- `maxSelections` (int, opcional): Máximo de seleções
- `title` (string, opcional): Título

**Retorna:** Lista de valores selecionados

**Exemplo de uso:**
```csharp
var options = new List<InteractionOption>
{
    new("Email", "email"),
    new("SMS", "sms"),
    new("Push", "push")
};

var selected = await ChooseMultipleOptionsAsync("Como prefere ser contactado?", options);
```

### 5. `AskUserForTextAsync`

Solicita entrada de texto livre.

**Parâmetros:**
- `message` (string): Instrução para o usuário
- `title` (string, opcional): Título
- `placeholder` (string, opcional): Texto de placeholder

**Retorna:** Texto digitado pelo usuário

**Exemplo de uso:**
```csharp
var name = await AskUserForTextAsync("Qual é o seu nome?", title: "Cadastro", placeholder: "Seu nome");
```

### 6. `ChooseWithCustomTextAsync`

Permite escolha de opção + texto personalizado.

**Parâmetros:**
- `message` (string): Mensagem de instrução
- `options` (List<InteractionOption>): Lista de opções
- `title` (string, opcional): Título
- `placeholder` (string, opcional): Texto de placeholder

**Retorna:** Valor da opção ou texto personalizado

**Exemplo de uso:**
```csharp
var options = new List<InteractionOption>
{
    new("Padrão", "default"),
    new("Personalizado", "custom")
};

var result = await ChooseWithCustomTextAsync("Escolha uma opção ou digite sua própria:", options);
```

### 7. `ShowProgressAsync`

Exibe barra de progresso.

**Parâmetros:**
- `operationName` (string): Nome da operação
- `total` (int): Total de itens
- `status` (string): Status atual

**Retorna:** Status da operação

**Exemplo de uso:**
```csharp
await ShowProgressAsync("Processando arquivos", 100, "Processando...");
```

## 📝 Configuração de DI

### Adicionando FlowStep ao seu projeto

```csharp
using FlowStep;

var services = new ServiceCollection()
    .AddFlowStep(McpMode.Cli); // Ou McpMode.Gui para GUI
```

## 🌐 Protocolo MCP

O servidor MCP usa o protocolo Model Context Protocol (MCP) para comunicação com LLMs.

### Transportes Suportados

1. **STDIO Transport**: Conexão via pipe
2. **HTTP Transport**: Conexão via HTTP com Server-Sent Events (SSE)

### Endpoint HTTP

```http
POST /api/mcp
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "NotifyUserAsync",
    "arguments": {
      "message": "Processamento concluído!",
      "title": "Atualização"
    }
  }
}
```

## 📚 Exemplos

Veja o arquivo `Program.cs` para exemplos de uso CLI.

Veja `McpServerProgram.cs` para exemplos de servidor MCP HTTP.

## 🔧 Dependências

- .NET 8.0
- Microsoft.Extensions.DependencyInjection 11.0.0
- Microsoft.SemanticKernel 1.70.0
- ModelContextProtocol 0.8.0-preview.1
- ModelContextProtocol.AspNetCore 0.8.0-preview.1

## 🎨 Tipos de Interação

### InteractionType

```csharp
public enum InteractionType
{
    Notification,       // Apenas exibe (OK)
    Confirmation,       // Sim/Não
    SingleChoice,       // ComboBox / Radio (Escolha 1)
    MultiChoice,        // Checkboxes (Escolha N)
    TextInput,          // Apenas texto
    ChoiceWithText      // Opções + Campo "Outros/Expandir"
}
```

## 📄 Licença

Este projeto é fornecido como está, sem garantias.