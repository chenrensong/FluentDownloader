using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentDownloader.NetworkFile;

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
            using (var stream = File.OpenRead(LocalPath))
            {
                list = await LoadM3U8FileSegmentsAsync(stream);
            }
            return list;
        }

        protected override Task<bool> LoadDownloadInfoAsync()
        {
            var flag = base.LoadDownloadInfoAsync();
            //本地文件路径
            var fileName = SuggestedFileName ?? DownloadInfo.ServerFileInfo.Name;
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            fileName = $"{fileNameWithoutExtension}.mp4";
            LocalFileFullPath = Path.Combine(DirectoryPath, fileName);
            DownloadInfoFileFullPath = Path.Combine(DirectoryPath, $"{fileName}.json");
            return flag;
        }

        public override async Task LoadAsync()
        {
            await base.LoadAsync();

        }

        private Task<IList<DownloadSegmentInfo>> LoadM3U8FileSegmentsAsync(Stream stream)
        {
            int Count = 0;
            Uri uri = new Uri(Url);
            IList<DownloadSegmentInfo> list = new List<DownloadSegmentInfo>();
            var reader = new StreamReader(stream);
            string line;
            //long bytes = 0;

            while ((line = reader.ReadLine()) != null)
            {
                string newUrl = string.Empty;
                if (line.StartsWith("http:"))
                {
                    newUrl = line;
                }
                else
                {
                    if (!line.StartsWith("#EXT"))
                    {
                        var schema = uri.AbsoluteUri.Substring(0, uri.AbsoluteUri.LastIndexOf("/"));
                        newUrl = $"{schema}/{line}";
                    }
                }
                if (!string.IsNullOrEmpty(newUrl))
                {
                    //var serverInfo = await ServerHelper.LoadServerInfoAsync(newUrl);
                    var downloadSegmentInfo = new DownloadSegmentInfo()
                    {
                        ID = Count++,
                        Url = newUrl,
                        //Start = bytes,
                        //End = bytes + serverInfo.Size,
                        //Size = serverInfo.Size,
                        TotalReadBytes = 0,
                        TempFile = Path.GetTempFileName()
                    };
                    list.Add(downloadSegmentInfo);
                    Console.WriteLine($"Start {downloadSegmentInfo.Start}  {list.Count}");
                    //bytes += serverInfo.Size;
                }
            }
            // Dictionary<string, ServerFileInfo> cache = new Dictionary<string, ServerFileInfo>();
            // Parallel.ForEach(list, (item) =>
            //{
            //    var serverInfo = ServerHelper.LoadServerInfoAsync(item.Url).Result;
            //    cache.Add(item.Url, serverInfo);
            //    Console.WriteLine($"Cache Count {cache.Count}");
            //});

            // foreach (var item in list)
            // {
            //     var serverInfo = cache[item.Url];
            //     item.Start = bytes;
            //     item.End = bytes + serverInfo.Size;
            //     item.Size = serverInfo.Size;
            //     bytes += serverInfo.Size;
            // }
            return Task.FromResult(list);
        }

        protected override async Task ReconstructSegmentsAsync()
        {

            if (DownloadInfo.Count == 0)
            {
                return;
            }

            using (Stream localFileStream = new FileStream(LocalFileFullPath, FileMode.Create, FileAccess.ReadWrite))
            {
                foreach (var Segment in DownloadInfo)
                {
                    localFileStream.Seek(Segment.Start, SeekOrigin.Begin);

                    using (Stream tempStream = new FileStream(Segment.TempFile, FileMode.Open, FileAccess.Read))
                    {
                        await tempStream.CopyToAsync(localFileStream);
                    }
                }
            }
            // Delete all the Temp files, after the reconstraction process.
            foreach (var Segment in DownloadInfo)
            {
                File.Delete(Segment.TempFile);
            }
        }

    }
}
