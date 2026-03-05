namespace BadgeFed.Models;

public class ProfileLink
{
    public string Type { get; set; } = "generic";
    public string Label { get; set; } = "";
    public string Uri { get; set; } = "";
    public int Order { get; set; } = 0;
}
