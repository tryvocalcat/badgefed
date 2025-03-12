using System.Text.Json.Serialization;

namespace ActivityPubDotNet.Core
{
    public class AcceptRequest
    {

        [JsonPropertyName("@context")]
        public string? Context { get; set; }
        public string? Id { get; set; }
        public string? Actor { get; set; }
        public dynamic? Object { get; set; }
    }
}