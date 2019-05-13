using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace FluentDownloader.NetworkFile
{
    public class DownloadInfo : List<DownloadSegmentInfo>
    {
        public DownloadInfo()
        {

        }

        public DownloadInfo(int capacity) : base(capacity)
        {

        }

        /// <summary>
        /// 远程文件属性(不保存信息）
        /// </summary>
        [JsonIgnore]
        public ServerFileInfo ServerFileInfo { get; set; }

        /// <summary>
        /// 一共读了多少内容
        /// </summary>
        [JsonIgnore]
        public long TotalReadBytes
        {
            get
            {
                var p = this.Select(m => m.TotalReadBytes);
                return p.Sum();
            }
        }

        [JsonIgnore]
        public long Size
        {
            get
            {
                var p = this.Select(m => m.Size);
                return p.Sum();
            }
        }


        /// <summary>
        /// 下载进度
        /// </summary>
        [JsonIgnore]
        public float Percentage
        {
            get
            {
                var p = this.Where(m => !float.IsNaN(m.Percentage)).Select(m => m.Percentage);
                var avg = p.Average();
                return avg;
            }
        }


    }
}
