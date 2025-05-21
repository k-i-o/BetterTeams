using System.Text.Json.Serialization;

namespace BetterTeams
{
    public class ManifestInfo
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("author")]
        public string? Author { get; set; }

        [JsonPropertyName("repository")]
        public string? Repository { get; set; }

        public AddonInfo ToAddonInfo(string addonFolderName)
        {
            return new AddonInfo
            {
                Id = string.IsNullOrEmpty(Id) ? GenerateAddonId(Name, addonFolderName) : Id,
                Name = Name ?? "Unknown Addon",
                Description = Description ?? "",
                Version = Version ?? "1.0.0",
                Author = Author ?? "Unknown Author",
                Repository = Repository ?? ""
            };
        }

        private string GenerateAddonId(string? name, string folderName)
        {
            string baseString = (name ?? "unknown") + "-" + folderName;

            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(baseString);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                return new Guid(hashBytes).ToString();
            }
        }
    }
}
