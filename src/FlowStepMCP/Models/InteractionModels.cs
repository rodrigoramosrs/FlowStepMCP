using System;
using System.Collections.Generic;

namespace FlowStep.Models
{
    // Tipos de interação suportados
    public enum InteractionType
    {
        Notification,       // Apenas exibe (OK)
        Confirmation,       // Sim/Não
        SingleChoice,       // ComboBox / Radio (Escolha 1)
        MultiChoice,        // Checkboxes (Escolha N)
        TextInput,          // Apenas texto
        ChoiceWithText      // Opções + Campo "Outros/Expandir"
    }

    // Uma opção individual (ex: Item de menu)
    public class InteractionOption
    {
        public string Label { get; set; }      // O que o usuário vê
        public string Value { get; set; }      // O valor retornado
        public bool IsDefault { get; set; }    // Selecionado por padrão
        public string? Description { get; set; } // Tooltip ou detalhe extra

        public InteractionOption(string label, string value, bool isDefault = false)
        {
            Label = label;
            Value = value;
            IsDefault = isDefault;
        }
    }

    // A Requisição Completa
    public class InteractionRequest
    {
        public string Title { get; set; } = "Sistema";
        public string Message { get; set; } = "";
        public InteractionType Type { get; set; }
        public List<InteractionOption>? Options { get; set; }

        // Configurações de Comportamento
        public TimeSpan? Timeout { get; set; }      // Tempo máximo para responder
        public bool AllowCustomInput { get; set; }  // Permite "Expandir" para texto livre
        public string CustomInputPlaceholder { get; set; } = "Digite aqui...";
        public bool IsCancellable { get; set; }     // Mostra botão Cancelar
        public int MinSelections { get; set; } = 0; // Para MultiChoice
        public int MaxSelections { get; set; } = 1; // Para MultiChoice

        // Helpers para criar requests rapidamente
        public static InteractionRequest Notify(string msg) => new() { Type = InteractionType.Notification, Message = msg };

        public static InteractionRequest Confirm(string msg) => new()
        {
            Type = InteractionType.Confirmation,
            Message = msg,
            Options = new() { new("Sim", "yes", true), new("Não", "no") }
        };

        public static InteractionRequest Choose(string msg, List<InteractionOption> opts, bool allowOther = false) => new()
        {
            Type = allowOther ? InteractionType.ChoiceWithText : InteractionType.SingleChoice,
            Message = msg,
            Options = opts,
            AllowCustomInput = allowOther
        };
    }

    // A Resposta do Usuário
    public class InteractionResponse
    {
        public bool Success { get; set; }
        public bool TimedOut { get; set; }
        public bool Cancelled { get; set; }

        public string? TextValue { get; set; }           // Para Input de Texto
        public List<string>? SelectedValues { get; set; } // Para Escolhas (Values das Options)
        public string? CustomInput { get; set; }         // Texto do campo "Expandir"
    }
}