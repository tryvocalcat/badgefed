using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace BadgeFed.Services
{
    public class EmailSettings
    {
        /// <summary>
        /// The email provider to use. Supported values: "Smtp", "Azure".
        /// </summary>
        public string Provider { get; set; } = "Smtp";

        // SMTP settings
        public string SmtpServer { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool EnableSsl { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        // Azure Communication Services settings
        public string AzureConnectionString { get; set; } = string.Empty;

        // Common settings
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
    }

    public class MailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly IEmailSender _emailSender;

        public MailService(IOptions<EmailSettings> emailSettings, IEmailSender emailSender)
        {
            _emailSettings = emailSettings.Value;
            _emailSender = emailSender;

            Console.WriteLine($"MailService initialized with provider: {_emailSettings.Provider}");
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
        {
            Console.WriteLine($"Sending email to {toEmail} with subject: {subject}");
            await _emailSender.SendAsync(toEmail, subject, body, _emailSettings.SenderEmail, _emailSettings.SenderName, isHtml);
        }

        public async Task SendEmailAsIssuerAsync(string toEmail, string subject, string body, string issuerName, bool isHtml = true)
        {
            var senderName = $"[{issuerName}] via {_emailSettings.SenderName}";
            Console.WriteLine($"Sending email to {toEmail} with subject: {subject} (sender: {senderName})");
            await _emailSender.SendAsync(toEmail, subject, body, _emailSettings.SenderEmail, senderName, isHtml);
        }

        public string ProcessTemplate(string template, Dictionary<string, string> variables)
        {
            var processedTemplate = template;
            
            foreach (var variable in variables)
            {
                var pattern = $"{{{variable.Key}}}";
                processedTemplate = Regex.Replace(processedTemplate, pattern, variable.Value);
            }

            return processedTemplate;
        }

        public async Task SendTemplatedEmailAsync(string toEmail, string subject, string template, Dictionary<string, string> variables, bool isHtml = true)
        {
            var processedBody = ProcessTemplate(template, variables);
            await SendEmailAsync(toEmail, subject, processedBody, isHtml);
        }

        public async Task SendTemplatedEmailAsIssuerAsync(string toEmail, string subject, string template, Dictionary<string, string> variables, string issuerName, bool isHtml = true)
        {
            var processedBody = ProcessTemplate(template, variables);
            await SendEmailAsIssuerAsync(toEmail, subject, processedBody, issuerName, isHtml);
        }
    }
}
