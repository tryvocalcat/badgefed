using System.ComponentModel.DataAnnotations;

namespace BadgeFed.Models;

public class Actor
{
    public long Id { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, ErrorMessage = "Name must be between {2} and {1} characters", MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Summary is required")]
    [StringLength(500, ErrorMessage = "Summary must not exceed {1} characters")]
    public string Summary { get; set; } = "";

    public string? AvatarPath { get; set; }

    public string? LinkedInOrganizationId { get; set; }

    public string Username { get; set; } = "";

    public bool IsMain { get; set; } = false; // New property

    public string OwnerId { get; set; } = string.Empty;

    public string? InformationUri { get; set; }

    public Uri? Uri
    {
        get
        {
            return new Uri($"https://{Domain}/actors/{Domain}/{Username}");
        }
    }

    public string? Domain { get; set; }

    public string? PublicKeyPem { get; set; }

    public string? PrivateKeyPem { get; set; }

    public string? Theme { get; set; } = "default";

    public bool ShowFollowers { get; set; } = true;

    public string? PublicKeyPemClean
    {
        get
        {
            // replace all \\n in PublicKeyPem for \n
            return PublicKeyPem?.Replace("\\n", "\n");
        }
    }

    public string? PrivateKeyPemClean
    {
        get
        {
            // replace all \\n in PrivateKeyPem for \n
            return PrivateKeyPem?.Replace("\\n", "\n");
        }
    }

    public string KeyId
    {
        get
        {
            return $"{Uri}#main-key";
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string FediverseHandle
    {
        get
        {
            return "@" + Username + "@" + Domain;
        }
    }


    public List<SocialUri> Socials { get; set; } = new();
}

public class SocialUri
{
    public string Type { get; set;  } = "generic";

    public string Uri { get; set; } = "";
}