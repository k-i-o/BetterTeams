using System.Text.Json.Serialization;

namespace BetterTeams
{
    public class AddonInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        [JsonPropertyName("repository")]
        public string Repository { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsActive { get; set; } = false;
    }


}
