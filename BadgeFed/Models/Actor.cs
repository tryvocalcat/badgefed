namespace BadgeFed.Models;

public class Actor
{
    public long Id { get; set; }
    //[Required(ErrorMessage = "Name is required")]
    //[StringLength(100, ErrorMessage = "Name must be between {2} and {1} characters", MinimumLength = 2)]
    public string FullName { get; set; } = "";
    
    //[Required(ErrorMessage = "Summary is required")]
    //[StringLength(500, ErrorMessage = "Summary must not exceed {1} characters")]
    public string Summary { get; set; } = "";
    
    public string? AvatarPath { get; set; }

    public Uri? InformationUri { get; set; }

    public string? Domain { get; set; }

    public string? PublicKeyPem { get; set; }
}