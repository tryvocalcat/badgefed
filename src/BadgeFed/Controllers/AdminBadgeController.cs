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
        private readonly EmailTemplateService _emailTemplateService;
        private readonly LocalScopedDb _localDbService;
        private readonly ILogger<AdminBadgeController> _logger;

        public AdminBadgeController(IConfiguration configuration, BadgeProcessor badgeProcessor, MailService mailService, EmailTemplateService emailTemplateService, LocalScopedDb localDbService, ILogger<AdminBadgeController> logger)
        {
            _configuration = configuration;
            _badgeProcessor = badgeProcessor;
            _mailService = mailService;
            _emailTemplateService = emailTemplateService;
            _localDbService = localDbService;
            _logger = logger;
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

            return Ok("Broadcasted badge successfully");
           // return Redirect("/admin/grants");
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

            var template = _emailTemplateService.GetBadgeProcessedTemplate(record.Actor.Id);
            var variables = new Dictionary<string, string>
            {
                { "recipientName", record.IssuedToName },
                { "badgeTitle", record.Title },
                { "badgeDescription", record.Description },
                { "issuerName", record.Actor.FullName },
                { "issuedDate", record.IssuedOn.ToString("MMMM dd, yyyy") },
                { "badgeLink", $"{record.NoteId}" }
            };

            try
            {
                _logger.LogInformation("[{RequestHost}] Sending processed badge email to {RecipientEmail} for badge {BadgeTitle}", Request.Host, recipientEmail, record.Title);

                await _mailService.SendTemplatedEmailAsIssuerAsync(
                    recipientEmail,
                    $"{record.Actor.FullName}: Your badge {record.Title} has been processed!",
                    template,
                    variables,
                    record.Actor.FullName,
                    true
                );

                _logger.LogInformation("[{RequestHost}] Successfully sent processed badge email to {RecipientEmail} for badge {BadgeTitle}", Request.Host, recipientEmail, record.Title);

                return Redirect("/admin/grants");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestHost}] Failed to send processed badge email to {RecipientEmail} for badge {BadgeTitle}", Request.Host, recipientEmail, record.Title);
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

            var template = _emailTemplateService.GetBadgeAwardTemplate(record.Actor.Id);

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
                _logger.LogInformation("[{RequestHost}] Sending badge award email to {RecipientEmail} for badge {BadgeTitle}", Request.Host, recipientEmail, record.Title);

                await _mailService.SendTemplatedEmailAsIssuerAsync(
                    recipientEmail,
                    $"{record.Actor.FullName}: You've been awarded the {record.Title} badge!",
                    template,
                    variables,
                    record.Actor.FullName,
                    true
                );

                _logger.LogInformation("[{RequestHost}] Successfully sent badge award email to {RecipientEmail} for badge {BadgeTitle}", Request.Host, recipientEmail, record.Title);

                return Redirect("/admin/grants");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestHost}] Failed to send badge award email to {RecipientEmail} for badge {BadgeTitle}", Request.Host, recipientEmail, record.Title);
                return StatusCode(500, $"Failed to send email notification: {ex.Message}");
                
            }
        }

        /** Process signs and create a badgenote **/
        [HttpGet("{id}/process")]
        public async Task<IActionResult> ProcessBadge(string id)
        {
            var recordId = long.Parse(id);
            
            _logger.LogInformation("[{RequestHost}] Starting badge processing for record ID: {RecordId}", Request.Host, recordId);
            
            try
            {
                var record = _badgeProcessor.SignAndGenerateBadge(recordId);
                
                if (record == null)
                {
                    _logger.LogWarning("[{RequestHost}] No badge found to process for record ID: {RecordId}", Request.Host, recordId);
                    return NotFound("No badges to process");
                }

                _logger.LogInformation("[{RequestHost}] Successfully processed badge for record ID: {RecordId}", Request.Host, recordId);

                // Redirect to the grants administration page after processing
                return Redirect("/admin/grants");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestHost}] Failed to process badge for record ID: {RecordId}", Request.Host, recordId);
                return StatusCode(500, $"Failed to process badge: {ex.Message}");
            }
        }
    }
}