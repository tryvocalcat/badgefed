using BadgeFed.Models;
using BadgeFed.Services;
using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers;

[ApiController]
[Route("api/profile")]
public class RecipientProfileController : ControllerBase
{
    private readonly RecipientProfileService _profileService;

    public RecipientProfileController(RecipientProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet("{slug}")]
    public IActionResult GetProfile(string slug)
    {
        var (recipient, badges) = _profileService.GetProfileBySlug(slug);
        if (recipient == null || !recipient.IsPublic)
        {
            return NotFound();
        }

        return Ok(new
        {
            name = recipient.Name,
            displayName = recipient.DisplayName,
            bio = recipient.Bio,
            avatarUrl = recipient.AvatarPath,
            headline = recipient.CustomHeadline,
            profileTemplate = recipient.ProfileTemplate,
            links = recipient.ProfileLinksList.Select(l => new
            {
                type = l.Type,
                label = l.Label,
                uri = l.Uri
            }),
            badges = badges.Select(b => new
            {
                title = b.Title,
                imageUrl = b.FullImageUrl,
                issuedOn = b.IssuedOn,
                issuer = b.Actor?.FullName,
                isVerified = !string.IsNullOrEmpty(b.FingerPrint)
            })
        });
    }
}
