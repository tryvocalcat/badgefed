using BadgeFed.Models;

namespace BadgeFed.Services
{
    public class BadgeGrantService
    {
        private readonly LocalScopedDb _localDbService;
        private readonly BadgeService _badgeService;
        private readonly ILogger<BadgeGrantService> _logger;

        public BadgeGrantService(LocalScopedDb localDbService, BadgeService badgeService, ILogger<BadgeGrantService> logger)
        {
            _localDbService = localDbService;
            _badgeService = badgeService;
            _logger = logger;
        }

        public class BadgeGrantRequest
        {
            public long BadgeId { get; set; }
            public string? ProfileUri { get; set; }
            public string? Name { get; set; }
            public string? Email { get; set; }
            public string? Evidence { get; set; }
        }

        public class BadgeGrantResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public string? Message { get; set; }
            public string? AcceptUrl { get; set; }
            public BadgeRecord? BadgeRecord { get; set; }
        }

        public BadgeGrantResult GrantBadge(BadgeGrantRequest request)
        {
            try
            {
                // Validate badge exists
                var badge = _localDbService.GetBadgeDefinitionById(request.BadgeId);
                if (badge == null)
                {
                    return new BadgeGrantResult 
                    { 
                        Success = false, 
                        ErrorMessage = $"Badge with ID {request.BadgeId} not found." 
                    };
                }

                // Validate recipient information
                if (string.IsNullOrEmpty(request.ProfileUri) && string.IsNullOrEmpty(request.Email))
                {
                    return new BadgeGrantResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Either Profile URI or Email must be provided." 
                    };
                }

                var recipient = new Recipient() 
                { 
                    ProfileUri = request.ProfileUri, 
                    Name = request.Name, 
                    Email = request.Email 
                };

                // Check for existing grants
                var sqlExisting = $"BadgeId = {request.BadgeId}";
                if (string.IsNullOrEmpty(request.ProfileUri))
                {
                    sqlExisting += $" AND IssuedToEmail = '{request.Email}'";
                }
                else
                {
                    sqlExisting += $" AND IssuedToSubjectUri = '{request.ProfileUri}'";
                }

                var existing = _localDbService.GetBadgeRecords(sqlExisting);
                if (existing != null && existing.Count > 0)
                {
                    return new BadgeGrantResult 
                    { 
                        Success = false, 
                        ErrorMessage = $"Badge {badge.Title} has already been granted to this recipient." 
                    };
                }

                // Create badge record
                var badgeRecord = _badgeService.GetGrantBadgeRecord(badge, recipient);
                
                // Set evidence if provided, otherwise use badge's earning criteria
                badgeRecord.EarningCriteria = !string.IsNullOrEmpty(request.Evidence) ? request.Evidence : badge.EarningCriteria;

                _localDbService.CreateBadgeRecord(badgeRecord);

                // Generate accept URL
                var acceptUrl = GenerateAcceptUrl(badgeRecord);

                _logger.LogInformation("Badge {BadgeTitle} granted to {RecipientName} ({RecipientProfileUri})", 
                    badgeRecord.Title, recipient.Name, recipient.ProfileUri);

                return new BadgeGrantResult
                {
                    Success = true,
                    Message = $"Badge {badgeRecord.Title} granted successfully.",
                    AcceptUrl = acceptUrl,
                    BadgeRecord = badgeRecord
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting badge {BadgeId}", request.BadgeId);
                return new BadgeGrantResult 
                { 
                    Success = false, 
                    ErrorMessage = "An error occurred while granting the badge." 
                };
            }
        }

        private string GenerateAcceptUrl(BadgeRecord badgeRecord)
        {
            // Generate the accept URL - you may need to adjust this based on your routing
            if (!string.IsNullOrEmpty(badgeRecord.AcceptKey))
            {
                return $"/accept/grant/{badgeRecord.Id}/{badgeRecord.AcceptKey}";
            }
            return $"/accept/grant/{badgeRecord.Id}";
        }
    }
}
