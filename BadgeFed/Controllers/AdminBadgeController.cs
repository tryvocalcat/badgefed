using ActivityPubDotNet.Core;
using BadgeFed.Services;
using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("admin/grant")]
    public class AdminBadgeController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly BadgeProcessor _badgeProcessor;
        private readonly MailService _mailService;
        private readonly LocalDbService _localDbService;

        public AdminBadgeController(IConfiguration configuration, BadgeProcessor badgeProcessor, MailService mailService, LocalDbService localDbService)
        {
            _configuration = configuration;
            _badgeProcessor = badgeProcessor;
            _mailService = mailService;
            _localDbService = localDbService;
        }

        [HttpGet("{id}/broadcast")]
        public async Task<IActionResult> BroadcastBadge(string id)
        {
            var recordId = long.Parse(id);
            
            var record = _badgeProcessor.BroadcastGrant(recordId);

            if (record == null)
            {
                return NotFound("No badges to broadcast");
            }

            return Redirect("/admin/grants");
        }

        [HttpGet("{id}/notify/activitypub")]
        public async Task<IActionResult> NotifyAcceptLinkByActivityPub(string id)
        {
            var recordId = long.Parse(id);

            var record = _badgeProcessor.NotifyGrantAcceptLink(recordId);

            if (record == null)
            {
                return NotFound("No badges to notify");
            }

            return Redirect("/admin/grants");
        }

        [HttpGet("{id}/notify-processed/activitypub")]
        public async Task<IActionResult> NotifyProcessedGrantActivityPub(string id)
        {
            var recordId = long.Parse(id);

            var record = _badgeProcessor.NotifyProcessedGrant(recordId);

            if (record == null)
            {
                return NotFound("No badges to notify");
            }

            return Redirect("/admin/grants");
        }

        
        [HttpGet("{id}/notify-processed/email")]
        public async Task<IActionResult> NotifyProcessedGrantEmail(string id, [FromQuery] string? email = null)
        {
            var recordId = long.Parse(id);

            var records = _localDbService.GetBadgeRecords(recordId);
            var record = records.FirstOrDefault();

            if (record == null)
            {
                return NotFound("No badges to notify");
            }

            record.Actor = _localDbService.GetActorByFilter($"Uri = \"{record.IssuedBy}\"")!;

            var recipientEmail = email ?? record.IssuedToEmail;

            if (string.IsNullOrEmpty(recipientEmail))
            {
                return BadRequest("No email address available for notification");
            }

            var template = @"
                <h1>Your Badge Has Been Processed!</h1>
                
                <p>Your badge has been successfully processed and is now available for sharing.</p>
                
                <p><strong>Badge Details:</strong></p>
                <ul>
                    <li>Title: {badgeTitle}</li>
                    <li>Description: {badgeDescription}</li>
                    <li>Issued By: {issuerName}</li>
                    <li>Issued On: {issuedDate}</li>
                </ul>
                
                <p>You can view your badge here:</p>
                <p><a href='{badgeLink}' class='button'>View Badge</a></p>
                
                <p>Best regards,<br>
                The BadgeFed Team</p>";
            var variables = new Dictionary<string, string>
            {
                { "recipientName", record.IssuedToName },
                { "badgeTitle", record.Title },
                { "badgeDescription", record.Description },
                { "issuerName", record.Actor.FullName },
                { "issuedDate", record.IssuedOn.ToString("MMMM dd, yyyy") },
                { "badgeLink", $"https://{record.Actor.Domain}/view/grant/{record.NoteId}" }
            };

            try
            {
                Console.WriteLine($"Sending email to {recipientEmail} for badge {record.Title}");

                await _mailService.SendTemplatedEmailAsync(
                    recipientEmail,
                    $"Your badge {record.Title} has been processed!",
                    template,
                    variables,
                    true
                );

                Console.WriteLine($"Email sent to {recipientEmail} for badge {record.Title}");

                return Redirect("/admin/grants");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StatusCode(500, $"Failed to send email notification: {ex.Message}");
            }
        }

        [HttpGet("{id}/notify/email")]
        public async Task<IActionResult> NotifyAcceptLinkByEmail(string id, [FromQuery] string? email = null)
        {
            var recordId = long.Parse(id);
            var records = _localDbService.GetBadgeRecords(recordId);

            var record = records.FirstOrDefault();

            if (record == null)
            {
                return NotFound("No badges to notify");
            }

            record.Actor = _localDbService.GetActorByFilter($"Uri = \"{record.IssuedBy}\"")!;

            var recipientEmail = email ?? record.IssuedToEmail;

            if (string.IsNullOrEmpty(recipientEmail))
            {
                return BadRequest("No email address available for notification");
            }

            var template = @"
                <h1>Congratulations on Your Badge Award!</h1>
                
                <p>You have been awarded the <strong>{badgeTitle}</strong> badge!</p>
                
                <p><strong>Badge Details:</strong></p>
                <ul>
                    <li>Title: {badgeTitle}</li>
                    <li>Description: {badgeDescription}</li>
                    <li>Issued By: {issuerName}</li>
                    <li>Issued On: {issuedDate}</li>
                </ul>
                
                <p>To accept your badge, please click the following link:</p>
                <p><a href='{acceptLink}' class='button'>Accept Badge</a></p>
                <small>or copy paste {acceptLink} in your browser.</small>
                
                <p>This is a private notification. Please do not share this link with others.</p>
                
                <p>Best regards,<br>
                The BadgeFed Team</p>
            ";

            var variables = new Dictionary<string, string>
            {
                { "recipientName", record.IssuedToName },
                { "badgeTitle", record.Title },
                { "badgeDescription", record.Description },
                { "issuerName", record.Actor.FullName },
                { "issuedDate", record.IssuedOn.ToString("MMMM dd, yyyy") },
                { "acceptLink", $"https://{record.Actor.Domain}/accept/grant/{record.Id}/{record.AcceptKey}" }
            };

            try
            {
                Console.WriteLine($"Sending email to {recipientEmail} for badge {record.Title}");

                await _mailService.SendTemplatedEmailAsync(
                    recipientEmail,
                    $"You've been awarded the {record.Title} badge!",
                    template,
                    variables,
                    true
                );

                Console.WriteLine($"Email sent to {recipientEmail} for badge {record.Title}");

                return Redirect("/admin/grants");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StatusCode(500, $"Failed to send email notification: {ex.Message}");
                
            }
        }

        /** Process signs and create a badgenote **/
        [HttpGet("{id}/process")]
        public async Task<IActionResult> ProcessBadge(string id)
        {
            var recordId = long.Parse(id);
            
            var record = _badgeProcessor.SignAndGenerateBadge(recordId);
            
            if (record == null)
            {
                return NotFound("No badges to process");
            }

            // Redirect to the grants administration page after processing
            return Redirect("/admin/grants");
        }
    }
}