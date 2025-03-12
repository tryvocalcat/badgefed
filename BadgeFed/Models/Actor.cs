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

    public Uri? Uri { get; set; }

    public string? Domain { get; set; }

    public string? PublicKeyPem { get; set; }

    public string? PrivateKeyPem { get; set; }

    public string? PublicKeyPemClean { 
        get {
            // replace all \\n in PublicKeyPem for \n
            return PublicKeyPem?.Replace("\\n", "\n");
        }
    }

    public string? PrivateKeyPemClean { 
        get {
            // replace all \\n in PrivateKeyPem for \n
            return PrivateKeyPem?.Replace("\\n", "\n");
        }
    }

    public string KeyId { 
        get { 
            return $"{Uri}#main-key";
        }
    }
}