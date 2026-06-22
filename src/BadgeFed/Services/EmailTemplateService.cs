namespace BadgeFed.Services;

public class EmailTemplateService
{
    private readonly ICustomAssetPathService _assetPathService;

    public const string BadgeAwardTemplateFile = "badge-award.html";
    public const string BadgeProcessedTemplateFile = "badge-processed.html";

    public static readonly string DefaultBadgeAwardTemplate = @"
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
The BadgeFed Team</p>";

    public static readonly string DefaultBadgeProcessedTemplate = @"
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

    public EmailTemplateService(ICustomAssetPathService assetPathService)
    {
        _assetPathService = assetPathService;
    }

    private string GetTemplatesPath(long actorId)
    {
        return Path.Combine(_assetPathService.GetCustomAssetsPath(), "templates", actorId.ToString());
    }

    public string GetBadgeAwardTemplate(long actorId)
    {
        return LoadTemplate(actorId, BadgeAwardTemplateFile, DefaultBadgeAwardTemplate);
    }

    public string GetBadgeProcessedTemplate(long actorId)
    {
        return LoadTemplate(actorId, BadgeProcessedTemplateFile, DefaultBadgeProcessedTemplate);
    }

    public void SaveBadgeAwardTemplate(long actorId, string template)
    {
        SaveTemplate(actorId, BadgeAwardTemplateFile, template);
    }

    public void SaveBadgeProcessedTemplate(long actorId, string template)
    {
        SaveTemplate(actorId, BadgeProcessedTemplateFile, template);
    }

    private string LoadTemplate(long actorId, string fileName, string defaultTemplate)
    {
        var path = Path.Combine(GetTemplatesPath(actorId), fileName);
        if (File.Exists(path))
        {
            return File.ReadAllText(path);
        }
        return defaultTemplate;
    }

    private void SaveTemplate(long actorId, string fileName, string content)
    {
        var dir = GetTemplatesPath(actorId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content);
    }
}
