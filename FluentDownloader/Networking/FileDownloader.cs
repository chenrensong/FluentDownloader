using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FluentDownloader.NetworkFile;

namespace FluentDownloader.Networking
{
    class FileDownloader : Downloader
    {
        public FileDownloader(string url, string directoryPath) : base(url, directoryPath)
        {
        }

        public FileDownloader(string url, string directoryPath, int threadCount) : base(url, directoryPath, threadCount)
        {
        }

        protected override Task<IList<DownloadSegmentInfo>> LoadFileSegmentsAsync()
        {
            return base.LoadFileSegmentsAsync();
        }
    }
}
