namespace BadgeFed.Services
{
    public interface IEmailSender
    {
        Task SendAsync(string toEmail, string subject, string body, string senderEmail, string senderName, bool isHtml = true);
    }
}
