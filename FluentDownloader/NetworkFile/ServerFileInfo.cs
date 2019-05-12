using Newtonsoft.Json;
using System.IO;

namespace FluentDownloader.NetworkFile
{
    public class ServerFileInfo
    {
        [JsonProperty]
        internal string Name { get; set; }
        [JsonProperty]
        internal string Extension { get; set; }
        [JsonProperty]
        internal long Size { get; set; }
        [JsonProperty]
        internal string MediaType { get; set; }
        [JsonProperty]
        internal bool IsResumable { get; set; }
        [JsonProperty]
        internal long TotalReadBytes { get; set; }
        [JsonIgnore]
        internal Stream DownloadContent { get; set; }
    }
}
