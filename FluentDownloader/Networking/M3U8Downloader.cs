using FluentDownloader.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FluentDownloader.Networking
{
    public class M3U8Downloader
    {

        internal string Url { get; set; }

        internal string DirectoryPath { get; set; }

        private FileDownloader fileDownloader = null;

        public string SuggestedFileName { get; set; }

        public M3U8Downloader(string url, string directoryPath)
        {
            this.Url = url;
            this.DirectoryPath = directoryPath;
            fileDownloader = new FileDownloader(url, directoryPath);
        }

        public async Task LoadAsync()
        {
            fileDownloader.SuggestedFileName = SuggestedFileName;
            await fileDownloader.LoadAsync();
        }

        public async Task DownloadFileAsync(Action<ProgressInfo> progressAction, CancellationToken cancellationToken = default)
        {
            await fileDownloader.DownloadFileAsync((e) =>
            {
                //Console.WriteLine($"M3U8 Time:{e.Time} AverageSpeed:{e.AverageSpeed.SizeSuffix()} CurrentValue:{e.CurrentValue.SizeSuffix()} Speed:{e.Speed.SizeSuffix()} Percentage:{e.Percentage}");
            }, cancellationToken);
            var filePath = fileDownloader.LocalFileFullPath;
            var m3u8StreamDownloader = new M3U8StreamDownloader(Url, filePath, DirectoryPath);
            m3u8StreamDownloader.SuggestedFileName = SuggestedFileName;
            await m3u8StreamDownloader.LoadAsync();
            await m3u8StreamDownloader.DownloadFileAsync(progressAction, cancellationToken);
        }



    }
}
