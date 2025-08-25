using BadgeFed.Models;
using Microsoft.Extensions.Configuration;

namespace BadgeFed.Services;

public class InvitationService
{
    private readonly LocalScopedDb _localDbService;
    private readonly MailService _mailService;
    private readonly IConfiguration _configuration;

    public InvitationService(LocalScopedDb localDbService, MailService mailService, IConfiguration configuration)
    {
        _localDbService = localDbService;
        _mailService = mailService;
        _configuration = configuration;
    }

    public Invitation CreateInvitation(string email, string invitedByUserId, string role = "manager", int expirationDays = 7, string? notes = null)
    {
        var invitation = new Invitation
        {
            Email = email.ToLowerInvariant(),
            InvitedBy = invitedByUserId,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            Role = role,
            Notes = notes
        };

        _localDbService.UpsertInvitation(invitation);
        return invitation;
    }

    public async Task<bool> SendInvitationEmailAsync(Invitation invitation, string instanceName = "BadgeFed")
    {
        try
        {
            var invitingUser = _localDbService.GetUserById(invitation.InvitedBy);
            var inviterName = invitingUser != null ? $"{invitingUser.GivenName} {invitingUser.Surname}" : "Administrator";
            
            var baseUrl = GetBaseUrl();
            var invitationUrl = $"{baseUrl}/admin/login?invitationCode={invitation.Id}";

            var emailTemplate = @"
                <h1>You're Invited to Join {instanceName}!</h1>
                
                <p>Hello!</p>
                
                <p>{inviterName} has invited you to join <strong>{instanceName}</strong>, a decentralized digital badge platform where you can create and manage your own badge issuer.</p>
                
                <p><strong>What you'll be able to do:</strong></p>
                <ul>
                    <li>Create your own badge issuer profile</li>
                    <li>Design and issue digital badges</li>
                    <li>Manage badge recipients</li>
                    <li>Connect with other badge issuers in the federation</li>
                </ul>
                
                <p>To accept your invitation and get started, click the button below:</p>
                
                <p style='text-align: center; margin: 30px 0;'>
                    <a href='{invitationUrl}' 
                       style='background-color: #4f46e5; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; display: inline-block; font-weight: bold;'>
                        Accept Invitation
                    </a>
                </p>
                
                <p><small>Or copy and paste this link in your browser: {invitationUrl}</small></p>
                
                {notesSection}
                
                <p><strong>Important:</strong></p>
                <ul>
                    <li>This invitation expires on {expirationDate}</li>
                    <li>You'll need to sign in with either Mastodon or LinkedIn to complete the setup</li>
                    <li>This invitation is personal and should not be shared</li>
                </ul>
                
                <p>If you have any questions, feel free to reach out to {inviterName} or our support team.</p>
                
                <p>Welcome to the BadgeFed community!</p>
                
                <p>Best regards,<br>
                The {instanceName} Team</p>";

            var notesSection = !string.IsNullOrEmpty(invitation.Notes) 
                ? $"<p><strong>Personal message from {inviterName}:</strong><br><em>{invitation.Notes}</em></p>" 
                : "";

            var finalEmail = emailTemplate
                .Replace("{instanceName}", instanceName)
                .Replace("{inviterName}", inviterName)
                .Replace("{invitationUrl}", invitationUrl)
                .Replace("{expirationDate}", invitation.ExpiresAt.ToString("MMMM d, yyyy 'at' h:mm tt UTC"))
                .Replace("{notesSection}", notesSection);

            await _mailService.SendEmailAsync(
                invitation.Email,
                $"You're invited to join {instanceName}!",
                finalEmail
            );

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send invitation email: {ex.Message}");
            return false;
        }
    }

    public Invitation? ValidateAndGetInvitation(string invitationCode)
    {
        var invitation = _localDbService.GetInvitationById(invitationCode);
        Console.WriteLine($"Validating invitation code: {invitationCode}, Found: {invitation != null}, IsValid: {invitation?.IsValid}");
        return invitation?.IsValid == true ? invitation : null;
    }

    public void AcceptInvitation(string invitationCode, User user)
    {
        var invitation = ValidateAndGetInvitation(invitationCode);
        if (invitation == null)
        {
            throw new InvalidOperationException("Invalid or expired invitation code");
        }

        // Ensure the user has the role specified in the invitation
        user.Role = invitation.Role;
        
        // Create or update the user
        _localDbService.UpsertUser(user);
        
        // Mark the invitation as accepted
        _localDbService.AcceptInvitation(invitationCode, user.Id);
    }

    public List<Invitation> GetInvitations(string? filter = null)
    {
        return _localDbService.GetInvitations(filter);
    }

    public void DeactivateInvitation(string invitationId)
    {
        _localDbService.DeactivateInvitation(invitationId);
    }

    private string GetBaseUrl()
    {
        // Try to get base URL from configuration, fallback to a default
        var baseUrl = _configuration["BaseUrl"];
        if (string.IsNullOrEmpty(baseUrl))
        {
            // Fallback - try to construct from other config
            var domain = _configuration["Domain"] ?? "localhost:5000";
            baseUrl = $"https://{domain}";
        }
        return baseUrl.TrimEnd('/');
    }
}
