namespace BadgeFed.Models;

public class CustomNavLink
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Icon { get; set; } = "fas fa-external-link-alt";
    public string Target { get; set; } = "_self";
    public string? RequiredRole { get; set; }
    public bool Enabled { get; set; } = true;
}

public class CustomNavLinksConfig
{
    public List<CustomNavLink> NavLinks { get; set; } = new();
}
