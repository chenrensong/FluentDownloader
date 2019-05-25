using FluentDownloader.NetworkFile;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FluentDownloader.Networking
{
    public class M3U8StreamDownloader : Downloader
    {
        public string LocalPath { get; private set; }

        public M3U8StreamDownloader(string url, string localPath, string directoryPath) : base(url, directoryPath, 1)
        {
            LocalPath = localPath;
        }

        protected async override Task<IList<DownloadSegmentInfo>> LoadFileSegmentsAsync()
        {
            IList<DownloadSegmentInfo> list = null;
            var bytes = File.ReadAllBytes(LocalPath);
            using (var stream = new MemoryStream(bytes))
            {
                list = await LoadM3U8FileSegmentsAsync(stream);
            }
            return list;
        }

        protected override Task<bool> LoadDownloadInfoAsync(bool LoadSrc = true)
        {
            //本地文件路径
            var fileName = SuggestedFileName ?? DownloadInfo.ServerFileInfo.Name;
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            fileName = $"{fileNameWithoutExtension}.mp4";
            LocalFileFullPath = Path.Combine(DirectoryPath, fileName);
            DownloadInfoFileFullPath = Path.Combine(DirectoryPath, $"{fileName}.json");
            var flag = base.LoadDownloadInfoAsync(false);
            return flag;
        }

        public override async Task LoadAsync()
        {
            await base.LoadAsync();
            DownloadInfo.ServerFileInfo.Size = 0;
        }

        /// <summary>
        /// 加载M3U8分片内容
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        private Task<IList<DownloadSegmentInfo>> LoadM3U8FileSegmentsAsync(Stream stream)
        {
            int Count = 0;
            Uri uri = new Uri(Url);
            IList<DownloadSegmentInfo> list = new List<DownloadSegmentInfo>();
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string newUrl = string.Empty;
                    if (line.StartsWith("http:"))
                    {
                        newUrl = line;
                    }
                    else if (!line.StartsWith("#EXT"))
                    {
                        var schema = uri.AbsoluteUri.Substring(0, uri.AbsoluteUri.LastIndexOf("/"));
                        newUrl = $"{schema}/{line}";
                    }
                    if (!string.IsNullOrEmpty(newUrl))
                    {
                        var downloadSegmentInfo = new DownloadSegmentInfo()
                        {
                            ID = Count++,
                            Url = newUrl,
                            TotalReadBytes = 0,
                            TempFile = Path.GetTempFileName()
                        };
                        list.Add(downloadSegmentInfo);
                    }
                }
            }
            return Task.FromResult(list);
        }

        /// <summary>
        /// 重组分片
        /// </summary>
        /// <returns></returns>
        protected override async Task ReconstructSegmentsAsync()
        {
            if (DownloadInfo.Count == 0)
            {
                return;
            }
            using (Stream localFileStream = new FileStream(LocalFileFullPath, FileMode.Create, FileAccess.ReadWrite))
            {
                localFileStream.Seek(0, SeekOrigin.Begin);
                foreach (var Segment in DownloadInfo)
                {
                    using (Stream tempStream = new FileStream(Segment.TempFile, FileMode.Open, FileAccess.Read))
                    {
                        await tempStream.CopyToAsync(localFileStream);
                    }
                }
            }
            // Delete all the Temp files, after the reconstraction process.
            foreach (var Segment in DownloadInfo)
            {
                try
                {
                    File.Delete(Segment.TempFile);
                }
                catch (Exception ex)
                {
                    //ignore
                }
            }
        }

    }
}
