using BadgeFed.Models;
using BadgeFed.Services;
using Microsoft.Extensions.Logging;

namespace BadgeFed.Services
{
    public class TokenGrantService
    {
        private readonly LocalScopedDb _localDbService;
        private readonly BadgeGrantService _badgeGrantService;
        private readonly ILogger<TokenGrantService> _logger;

        public TokenGrantService(LocalScopedDb localDbService, BadgeGrantService badgeGrantService, ILogger<TokenGrantService> logger)
        {
            _localDbService = localDbService;
            _badgeGrantService = badgeGrantService;
            _logger = logger;
        }

        public class TokenRedemptionRequest
        {
            public string Name { get; set; } = string.Empty;
            public string? ProfileUri { get; set; }
            public string? IpAddress { get; set; }
            public string? UserAgent { get; set; }
        }

        public class TokenRedemptionResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public string? Message { get; set; }
            public string? AcceptUrl { get; set; }
            public BadgeRecord? BadgeRecord { get; set; }
            public TokenGrantRedemption? Redemption { get; set; }
        }

        public TokenRedemptionResult RedeemToken(string shortCode, TokenRedemptionRequest request)
        {
            try
            {
                // Get the token grant
                var tokenGrant = _localDbService.GetTokenGrantByShortCode(shortCode);
                if (tokenGrant == null)
                {
                    return new TokenRedemptionResult
                    {
                        Success = false,
                        ErrorMessage = "Token grant not found."
                    };
                }

                // Check if token grant can be redeemed
                if (!tokenGrant.CanRedeem)
                {
                    var reason = !tokenGrant.IsActive ? "Token grant is inactive." :
                               !tokenGrant.IsEnabled ? "Token grant is not yet enabled." :
                               !tokenGrant.HasRedemptionsLeft ? "Token grant has reached its redemption limit." :
                               "Token grant cannot be redeemed at this time.";

                    return new TokenRedemptionResult
                    {
                        Success = false,
                        ErrorMessage = reason
                    };
                }

                // Validate recipient information
                if (string.IsNullOrEmpty(request.ProfileUri))
                {
                    return new TokenRedemptionResult
                    {
                        Success = false,
                        ErrorMessage = "Profile URI must be provided."
                    };
                }

                // Create badge grant request
                var badgeGrantRequest = new BadgeGrantService.BadgeGrantRequest
                {
                    BadgeId = tokenGrant.BadgeId,
                    ProfileUri = request.ProfileUri?.Trim(),
                    Name = request.Name?.Trim()
                };

                // Grant the badge
                var grantResult = _badgeGrantService.GrantBadge(badgeGrantRequest);

                if (!grantResult.Success)
                {
                    return new TokenRedemptionResult
                    {
                        Success = false,
                        ErrorMessage = grantResult.ErrorMessage
                    };
                }

                // Create redemption record
                var redemption = new TokenGrantRedemption
                {
                    TokenGrantId = tokenGrant.Id,
                    BadgeRecordId = grantResult.BadgeRecord!.Id,
                    RecipientName = request.Name,
                    RecipientProfileUri = request.ProfileUri,
                    IpAddress = request.IpAddress,
                    UserAgent = request.UserAgent,
                    RedeemedAt = DateTime.UtcNow
                };

                _localDbService.CreateTokenGrantRedemption(redemption);

                _logger.LogInformation("Token grant {ShortCode} redeemed by {RecipientName} ({RecipientProfileUri})", 
                    shortCode, request.Name, request.ProfileUri);

                return new TokenRedemptionResult
                {
                    Success = true,
                    Message = $"Badge '{grantResult.BadgeRecord.Title}' has been granted successfully!",
                    AcceptUrl = grantResult.AcceptUrl,
                    BadgeRecord = grantResult.BadgeRecord,
                    Redemption = redemption
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error redeeming token grant {ShortCode}", shortCode);
                return new TokenRedemptionResult
                {
                    Success = false,
                    ErrorMessage = "An error occurred while redeeming the token. Please try again."
                };
            }
        }

        public TokenGrant? GetTokenGrantByShortCode(string shortCode)
        {
            return _localDbService.GetTokenGrantByShortCode(shortCode);
        }

        public bool ValidateShortCode(string shortCode)
        {
            if (string.IsNullOrWhiteSpace(shortCode))
                return false;

            // Allow only alphanumeric characters, hyphens, and underscores
            return shortCode.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
        }

        public string GenerateShortCode(string title)
        {
            // Generate a short code from the title
            var shortCode = title.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("_", "-");

            // Remove non-alphanumeric characters except hyphens
            shortCode = new string(shortCode.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

            // Remove consecutive hyphens and trim
            while (shortCode.Contains("--"))
                shortCode = shortCode.Replace("--", "-");

            shortCode = shortCode.Trim('-');

            // Ensure it's not too long
            if (shortCode.Length > 20)
                shortCode = shortCode.Substring(0, 20).TrimEnd('-');

            // Add suffix if needed to make it unique
            var originalShortCode = shortCode;
            var counter = 1;
            while (!_localDbService.IsShortCodeAvailable(shortCode))
            {
                shortCode = $"{originalShortCode}-{counter}";
                counter++;
            }

            return shortCode;
        }
    }
}