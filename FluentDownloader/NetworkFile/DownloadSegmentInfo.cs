using Newtonsoft.Json;
using System.IO;

namespace FluentDownloader.NetworkFile
{
    public class DownloadSegmentInfo
    {
        /// <summary>
        /// 下载ID
        /// </summary>
        [JsonProperty]
        public int ID { get; set; }
        /// <summary>
        /// 文件起始
        /// </summary>
        [JsonProperty]
        public long Start { get; set; }
        /// <summary>
        /// 文件结束
        /// </summary>
        [JsonProperty]
        public long End { get; set; }
        /// <summary>
        /// 文件大小
        /// </summary>
        [JsonProperty]
        public long Size { get; set; }
        /// <summary>
        /// 下载Url(如果用父的就不用设置）
        /// </summary>
        [JsonProperty]
        public string Url { get; set; }
        /// <summary>
        /// 所有已读的数据
        /// </summary>
        [JsonProperty]
        public long TotalReadBytes { get; set; }
        /// <summary>
        /// 下载百分比
        /// </summary>
        [JsonIgnore]
        public float Percentage
        {
            get
            {
                return (TotalReadBytes / (float)Size) * 100;
            }
        }
        /// <summary>
        /// 下载Stream
        /// </summary>
        [JsonIgnore]
        public Stream SrcStream { get; set; }
        /// <summary>
        /// 目标Stream
        /// </summary>
        [JsonIgnore]
        public Stream DstStream { get; set; }
    }
}
