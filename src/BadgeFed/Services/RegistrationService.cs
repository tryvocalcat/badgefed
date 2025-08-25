using System.Text.Json;
using System.Text.RegularExpressions;
using BadgeFed.Models;
using Microsoft.Extensions.Logging;

namespace BadgeFed.Services
{
    public class RegistrationService
    {
        private readonly LocalScopedDb _db;
        private readonly ILogger<RegistrationService> _logger;
        private readonly IWebHostEnvironment _environment;

        public RegistrationService(LocalScopedDb db, ILogger<RegistrationService> logger, IWebHostEnvironment environment)
        {
            _db = db;
            _logger = logger;
            _environment = environment;
        }

        public FormConfiguration GetFormConfiguration()
        {
            try
            {
                // First check for custom form.json in a config directory
                var customConfigPath = Path.Combine(_environment.ContentRootPath, "config", "form.json");
                if (File.Exists(customConfigPath))
                {
                    var customJson = File.ReadAllText(customConfigPath);
                    return JsonSerializer.Deserialize<FormConfiguration>(customJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
                           ?? GetDefaultConfiguration();
                }

                // Fall back to default form.json in wwwroot
                var defaultConfigPath = Path.Combine(_environment.WebRootPath, "form.json");
                if (File.Exists(defaultConfigPath))
                {
                    var defaultJson = File.ReadAllText(defaultConfigPath);
                    return JsonSerializer.Deserialize<FormConfiguration>(defaultJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? GetDefaultConfiguration();
                }

                return GetDefaultConfiguration();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading form configuration, using default");
                return GetDefaultConfiguration();
            }
        }

        private FormConfiguration GetDefaultConfiguration()
        {
            return new FormConfiguration
            {
                Title = "BadgeFed Registration",
                Description = "Join our BadgeFed instance by filling out the form below.",
                SubmitButtonText = "Submit Registration",
                Fields = new List<FormField>
                {
                    new FormField
                    {
                        Name = "firstName",
                        Label = "First Name",
                        Type = "text",
                        Required = true,
                        Placeholder = "Enter your first name"
                    },
                    new FormField
                    {
                        Name = "lastName",
                        Label = "Last Name",
                        Type = "text",
                        Required = true,
                        Placeholder = "Enter your last name"
                    },
                    new FormField
                    {
                        Name = "email",
                        Label = "Email Address",
                        Type = "email",
                        Required = true,
                        Placeholder = "Enter your email address"
                    }
                }
            };
        }

        public FormSubmission ValidateSubmission(Dictionary<string, object> formData, FormConfiguration config)
        {
            var submission = new FormSubmission { Data = formData };

            foreach (var field in config.Fields)
            {
                var value = formData.ContainsKey(field.Name) ? formData[field.Name] : null;
                var stringValue = value?.ToString() ?? string.Empty;

                // Required field validation
                if (field.Required)
                {
                    if (field.Type == "checkbox")
                    {
                        if (value == null || !bool.TryParse(stringValue, out var boolValue) || !boolValue)
                        {
                            submission.ValidationErrors.Add($"{field.Label} is required.");
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(stringValue))
                    {
                        submission.ValidationErrors.Add($"{field.Label} is required.");
                    }
                }

                // Type-specific validation
                if (!string.IsNullOrWhiteSpace(stringValue))
                {
                    switch (field.Type)
                    {
                        case "email":
                            if (!IsValidEmail(stringValue))
                            {
                                submission.ValidationErrors.Add($"{field.Label} must be a valid email address.");
                            }
                            break;

                        case "select":
                            if (field.Options != null && !field.Options.Any(o => o.Value == stringValue))
                            {
                                submission.ValidationErrors.Add($"{field.Label} contains an invalid selection.");
                            }
                            break;
                    }

                    // Length validation
                    if (field.MaxLength.HasValue && stringValue.Length > field.MaxLength.Value)
                    {
                        submission.ValidationErrors.Add($"{field.Label} cannot exceed {field.MaxLength.Value} characters.");
                    }

                    // Pattern validation
                    if (!string.IsNullOrWhiteSpace(field.ValidationPattern))
                    {
                        if (!Regex.IsMatch(stringValue, field.ValidationPattern))
                        {
                            var message = field.ValidationMessage ?? $"{field.Label} format is invalid.";
                            submission.ValidationErrors.Add(message);
                        }
                    }
                }
            }

            return submission;
        }

        public async Task<int> SaveRegistrationAsync(FormSubmission submission, string? ipAddress = null, string? userAgent = null)
        {
            var formDataJson = JsonSerializer.Serialize(submission.Data);
            
            // Extract common fields for easy querying
            var email = submission.Data.ContainsKey("email") ? submission.Data["email"]?.ToString() : null;
            var firstName = submission.Data.ContainsKey("firstName") ? submission.Data["firstName"]?.ToString() : null;
            var lastName = submission.Data.ContainsKey("lastName") ? submission.Data["lastName"]?.ToString() : null;
            var name = !string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName) 
                ? $"{firstName} {lastName}" 
                : firstName ?? lastName;

            var registration = new Registration
            {
                FormData = formDataJson,
                Email = email,
                Name = name,
                IpAddress = ipAddress ?? string.Empty,
                UserAgent = userAgent ?? string.Empty,
                SubmittedAt = DateTime.UtcNow
            };

            return await _db.CreateRegistrationAsync(registration);
        }

        public async Task<List<Registration>> GetRegistrationsAsync(bool? reviewed = null, bool? approved = null)
        {
            return await _db.GetRegistrationsAsync(reviewed, approved);
        }

        public async Task<Registration?> GetRegistrationAsync(int id)
        {
            return await _db.GetRegistrationByIdAsync(id);
        }

        public async Task<bool> ReviewRegistrationAsync(int id, bool approved, string? notes = null, string? reviewedBy = null)
        {
            return await _db.ReviewRegistrationAsync(id, approved, notes, reviewedBy);
        }

        public async Task<bool> SaveFormConfigurationAsync(FormConfiguration config)
        {
            try
            {
                var configJson = JsonSerializer.Serialize(config, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // Ensure config directory exists
                var configDir = Path.Combine(_environment.ContentRootPath, "config");
                Directory.CreateDirectory(configDir);

                // Save to custom config file
                var configPath = Path.Combine(configDir, "form.json");
                await File.WriteAllTextAsync(configPath, configJson);

                _logger.LogInformation("Form configuration saved to {ConfigPath}", configPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving form configuration");
                return false;
            }
        }

        public async Task<bool> ResetFormConfigurationAsync()
        {
            try
            {
                var configPath = Path.Combine(_environment.ContentRootPath, "config", "form.json");
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                    _logger.LogInformation("Custom form configuration deleted, will use default");
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting form configuration");
                return false;
            }
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
                return emailRegex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }
    }
}
