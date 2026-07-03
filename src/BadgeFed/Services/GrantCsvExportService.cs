using System.Globalization;
using System.Text;
using BadgeFed.Models;

namespace BadgeFed.Services
{
    public class GrantCsvExportService
    {
        private static readonly string[] DefaultColumns =
        {
            "Id",
            "Title",
            "Status",
            "IssuedToName",
            "IssuedToEmail",
            "IssuedToSubjectUri",
            "IssuedBy",
            "IssuedOn",
            "AcceptedOn",
            "LastUpdated",
            "Description",
            "FingerPrint",
            "NoteId",
            "IsExternal",
            "Visibility"
        };

        public string BuildCsv(IEnumerable<BadgeRecord> grants)
        {
            ArgumentNullException.ThrowIfNull(grants);

            var csv = new StringBuilder();
            AppendRow(csv, DefaultColumns);

            foreach (var grant in grants)
            {
                if (grant == null)
                {
                    continue;
                }

                AppendRow(csv, new[]
                {
                    grant.Id.ToString(),
                    grant.Title,
                    grant.Status,
                    grant.IssuedToName,
                    grant.IssuedToEmail,
                    grant.IssuedToSubjectUri,
                    grant.IssuedBy,
                    FormatDate(grant.IssuedOn),
                    FormatDate(grant.AcceptedOn),
                    FormatDate(grant.LastUpdated),
                    grant.Description,
                    grant.FingerPrint,
                    grant.NoteId,
                    grant.IsExternal.ToString(),
                    grant.Visibility
                });
            }

            return csv.ToString();
        }

        private static void AppendRow(StringBuilder csv, IEnumerable<string> values)
        {
            var row = string.Join(",", values.Select(EscapeCsvValue));
            csv.AppendLine(row);
        }

        private static string EscapeCsvValue(string? value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
            if (normalized.Contains('"') || normalized.Contains(',') || normalized.Contains('\n'))
            {
                return $"\"{normalized.Replace("\"", "\"\"")}\"";
            }

            return normalized;
        }

        private static string FormatDate(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : string.Empty;
        }
    }
}
