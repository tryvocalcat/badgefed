using System.Net.Mail;

namespace BadgeFed.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpClient _smtpClient;

        public SmtpEmailSender(string smtpServer, int port, bool enableSsl, string username, string password)
        {
            _smtpClient = new SmtpClient(smtpServer, port)
            {
                Credentials = new System.Net.NetworkCredential(username, password),
                EnableSsl = enableSsl
            };
        }

        public async Task SendAsync(string toEmail, string subject, string body, string senderEmail, string senderName, bool isHtml = true)
        {
            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            mailMessage.To.Add(toEmail);
            await _smtpClient.SendMailAsync(mailMessage);
        }
    }
}
