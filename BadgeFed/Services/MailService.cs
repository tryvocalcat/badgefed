using System.Net.Mail;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace BadgeFed.Services
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool EnableSsl { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
    }

    public class MailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly SmtpClient _smtpClient;

        public MailService(IOptions<EmailSettings> emailSettings)
        {
            Console.WriteLine($"Initializing MailService with settings: {emailSettings.Value.SmtpServer}, {emailSettings.Value.Port}, {emailSettings.Value.EnableSsl}");
            Console.WriteLine($"Sender Email: {emailSettings.Value.SenderEmail}, Sender Name: {emailSettings.Value.SenderName}");

            _emailSettings = emailSettings.Value;

            Console.WriteLine($"Username: {_emailSettings.Username} Password: {_emailSettings.Password} Port: {_emailSettings.Port} SenderEmail: {_emailSettings.SenderEmail} SenderName: {_emailSettings.SenderName}");
            
            _smtpClient = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port)
            {
                Credentials = new System.Net.NetworkCredential(_emailSettings.Username, _emailSettings.Password),
                EnableSsl = _emailSettings.EnableSsl
            };
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
        {
            Console.WriteLine($"Sending email to {toEmail} with subject: {subject}");
            Console.WriteLine($"Email body: {body}");
            var mailMessage = new MailMessage
            {
                From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            mailMessage.To.Add(toEmail);

            Console.WriteLine($"Sending email to {toEmail} with subject: {subject}");

            await _smtpClient.SendMailAsync(mailMessage);
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
    }
}
