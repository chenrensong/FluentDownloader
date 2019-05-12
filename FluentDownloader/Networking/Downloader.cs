using FluentDownloader.Extensions;
using FluentDownloader.Internal;
using FluentDownloader.NetworkFile;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace FluentDownloader.Networking
{

    public class Downloader
    {

        public Downloader(string url, string directoryPath, int threadCount)
        {
            this.ThreadCount = threadCount;
            this.Url = url;
            this.DirectoryPath = directoryPath;
            DownloadInfo = new DownloadInfo(threadCount);
            FileSegmentaionTasks = new List<Task>(threadCount);
        }

        public Downloader(string url, string directoryPath)
            : this(url, directoryPath, Environment.ProcessorCount)
        {
        }

        /// <summary>
        /// 线程数
        /// </summary>
        protected int ThreadCount { get; set; }

        internal DownloadInfo DownloadInfo { get; private set; }

        internal IList<Task> FileSegmentaionTasks { get; private set; }

        internal string Url { get; set; }

        internal string DirectoryPath { get; set; }
        /// <summary>
        /// Giving a name to overwrite the remote suggested file name.
        /// </summary>
        public string SuggestedFileName { get; set; }

        internal string LocalFileFullPath { get; set; }

        internal string DownloadInfoFileFullPath { get; set; }

        public Mode DownloadMode { get; set; } = Mode.FileExistsStopDownload;

        public enum Mode
        {
            /// <summary>
            /// 文件已经下载完成不再下载
            /// </summary>
            FileExistsStopDownload,
            /// <summary>
            /// 文件已经下载完成重新下载
            /// </summary>
            FileExistsForceToReplace,
            /// <summary>
            /// 文件已经下载完成重新新建下载
            /// </summary>
            FileExistsToNewCreate
        }


        private IEnumerable<(long Start, long End)> SegmentPosition(long ContentLength, int ChunksNumber)
        {
            long PartSize = (long)Math.Ceiling(ContentLength / (double)ChunksNumber);
            for (var i = 0; i < ChunksNumber; i++)
            {
                yield return (i * PartSize + Math.Min(1, i), Math.Min((i + 1) * PartSize, ContentLength));
            }
        }

        /// <summary>
        /// 检查参数
        /// </summary>
        private void CheckArgument()
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                throw new ArgumentNullException("Url", "Can't let Url to be empty!");
            }
            if (!(Url.StartsWith("https://") || Url.StartsWith("http://")))
            {
                throw new Exception("Only Support Http, Https protocols");
            }
        }

        private async Task<bool> IsResumable(string url)
        {
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Range = new RangeHeaderValue(1, 1);
                    using (HttpResponseMessage Result = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (Result.StatusCode == HttpStatusCode.PartialContent)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 加载文件信息
        /// </summary>
        /// <returns></returns>
        private async Task<ServerFileInfo> LoadServerInfoAsync()
        {
            using (HttpClient httpClient = new HttpClient())
            {
                HttpResponseMessage httpResponseMessage = await httpClient.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead);
                if (httpResponseMessage.IsSuccessStatusCode == false)
                {
                    throw new Exception(httpResponseMessage.ReasonPhrase);
                }
                //Console.WriteLine($"Get responseHeadersRead {stopwatch.ElapsedMilliseconds}");
                var isResumable = await IsResumable(Url);
                //Console.WriteLine($"Get isResumable {stopwatch.ElapsedMilliseconds}");
                var downloadContent = await httpResponseMessage.Content.ReadAsStreamAsync();
                //Console.WriteLine($"Get downloadContent {stopwatch.ElapsedMilliseconds}");
                return new ServerFileInfo
                {
                    Name = httpResponseMessage.Content.Headers?.ContentDisposition?.FileName ?? httpResponseMessage.RequestMessage.RequestUri.Segments.LastOrDefault(),
                    MediaType = httpResponseMessage.Content.Headers.ContentType.MediaType,
                    Size = httpResponseMessage.Content.Headers.ContentLength.GetValueOrDefault(),
                    Extension = httpResponseMessage.Content.Headers.ContentType.MediaType.GetFileExtension(),
                    IsResumable = isResumable,
                    DownloadContent = downloadContent,
                    TotalReadBytes = 0
                };
            }
        }

        /// <summary>
        /// 加载文件分片信息
        /// </summary>
        /// <returns></returns>
        protected virtual async Task<IList<DownloadSegmentInfo>> LoadFileSegmentsAsync()
        {
            var list = new List<DownloadSegmentInfo>();
            int count = 0;
            using (HttpClient httpClient = new HttpClient())
            {
                var chunkCount = ThreadCount;
                //不支持Resume
                if (DownloadInfo.ServerFileInfo == null || !DownloadInfo.ServerFileInfo.IsResumable)
                {
                    chunkCount = 1;
                }
                foreach (var (Start, End) in SegmentPosition(DownloadInfo.ServerFileInfo.Size, chunkCount))
                {
                    httpClient.DefaultRequestHeaders.Range = new RangeHeaderValue(Start, End);
                    list.Add(new DownloadSegmentInfo
                    {
                        ID = count++,
                        SrcStream = await httpClient.GetStreamAsync(Url),
                        Start = Start,
                        End = End,
                        Size = End - Start,
                        TotalReadBytes = 0,
                    });
                }
            }
            return list;
        }

        //还原下载信息
        private async Task<bool> LoadDownloadInfoAsync()
        {
            try
            {
                if (File.Exists(DownloadInfoFileFullPath))
                {
                    var downloadInfoText = File.ReadAllText(DownloadInfoFileFullPath);
                    var downloadInfo = JsonConvert.DeserializeObject<DownloadInfo>(downloadInfoText);
                    using (HttpClient httpClient = new HttpClient())
                    {
                        foreach (var item in downloadInfo)
                        {
                            if (item.TotalReadBytes < item.Size)
                            {
                                httpClient.DefaultRequestHeaders.Range = new RangeHeaderValue(item.Start + item.TotalReadBytes, item.End);
                                item.SrcStream = await httpClient.GetStreamAsync(item.Url ?? Url);
                            }
                        }
                    }
                    DownloadInfo.AddRange(downloadInfo);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private Task<bool> SaveDownloadInfoAsync()
        {
            try
            {
                var downloadInfoText = JsonConvert.SerializeObject(DownloadInfo);
                if (!string.IsNullOrEmpty(downloadInfoText))
                {
                    File.WriteAllText(DownloadInfoFileFullPath, downloadInfoText);
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 下载完成
        /// </summary>
        /// <returns></returns>
        private async Task<bool> CompleteAsync()
        {
            try
            {
                if (DownloadInfo.Percentage >= 100)
                {
                    File.Delete(DownloadInfoFileFullPath);
                }
                else
                {
                    await SaveDownloadInfoAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }


        public async Task LoadAsync()
        {
            //检查参数
            CheckArgument();
            //获取Server文件信息
            DownloadInfo.ServerFileInfo = await LoadServerInfoAsync();
            //本地文件路径
            var fileName = SuggestedFileName ?? DownloadInfo.ServerFileInfo.Name;
            //bool fileExists = File.Exists(LocalFileFullPath);
            //文件夹不存在，创建文件夹
            if (!Directory.Exists(DirectoryPath))
            {
                Directory.CreateDirectory(DirectoryPath);
            }
            LocalFileFullPath = Path.Combine(DirectoryPath, fileName);
            DownloadInfoFileFullPath = Path.Combine(DirectoryPath, $"{fileName}.json");
            var loadSuccess = await LoadDownloadInfoAsync();
            ///没有成功需要重新加载
            if (!loadSuccess)
            {
                //拆分分片
                var fileSegments = await LoadFileSegmentsAsync();
                DownloadInfo.AddRange(fileSegments);
                await SaveDownloadInfoAsync();
            }
        }


        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="progressAction"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DownloadFileAsync(Action<ProgressInfo> progressAction, CancellationToken cancellationToken = default)
        {
            SpeedCalculator speedCalculator = new SpeedCalculator();
            ProgressInfo progressInfo = new ProgressInfo();
            speedCalculator.Updated += async (h) =>
            {
                progressInfo.AverageSpeed = speedCalculator.AverageSpeed;
                progressInfo.CurrentValue = speedCalculator.CurrentValue;
                progressInfo.Speed = speedCalculator.Speed;
                progressInfo.TargetValue = DownloadInfo.ServerFileInfo.Size;
                progressInfo.Percentage = DownloadInfo.Percentage;
                progressAction.Invoke(progressInfo);
                await SaveDownloadInfoAsync();
                if (progressInfo.Percentage >= 100)
                {
                    speedCalculator.Stop();
                    await CompleteAsync();
                }
            };
            foreach (var segment in DownloadInfo)
            {
                if (segment.SrcStream == null)
                {
                    continue;
                }
                var task = DownloadSegmentFileAsync(segment, (r, percentage) =>
                {
                    speedCalculator.CurrentValue += r;
                }, cancellationToken);
                FileSegmentaionTasks.Add(task);
            }
            speedCalculator.Start();
            await Task.WhenAll(FileSegmentaionTasks);
        }

        /// <summary>
        /// 下载分片
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="progressAction"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected virtual Task DownloadSegmentFileAsync(DownloadSegmentInfo segment, Action<long, float> progressAction, CancellationToken cancellationToken = default)
        {
            var localFile = new FileStream(LocalFileFullPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            localFile.Position = segment.Start + segment.TotalReadBytes;
            segment.DstStream = localFile;
            var task = segment.DownloadAsync(progressAction, cancellationToken);
            return task;
        }

    }
}



