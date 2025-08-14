using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BadgeFed.Models
{
    public class FormConfiguration
    {
        public string Title { get; set; } = "Registration Form";
        public string Description { get; set; } = "Please fill out the form below to register.";
        public string SubmitButtonText { get; set; } = "Submit Registration";
        public List<FormField> Fields { get; set; } = new();
    }

    public class FormField
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = "text"; // text, email, url, textarea, checkbox, checkboxGroup, select
        public bool Required { get; set; } = false;
        public string? Placeholder { get; set; }
        public string? HelpText { get; set; }
        public List<SelectOption>? Options { get; set; } // For select and checkboxGroup fields
        public string? DefaultValue { get; set; }
        public int? MaxLength { get; set; }
        public int? Rows { get; set; } // For textarea fields
        public string? ValidationPattern { get; set; } // Regex pattern for validation
        public string? ValidationMessage { get; set; }
    }

    public class SelectOption
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class FormSubmission
    {
        public Dictionary<string, object> Data { get; set; } = new();
        public List<string> ValidationErrors { get; set; } = new();
        public bool IsValid => !ValidationErrors.Any();
    }
}
