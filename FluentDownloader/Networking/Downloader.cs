using FluentDownloader.Extensions;
using FluentDownloader.Internal;
using FluentDownloader.NetworkFile;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
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

        internal List<Task> FileSegmentaionTasks { get; private set; }

        internal string Url { get; set; }

        internal string DirectoryPath { get; set; }
        /// <summary>
        /// Giving a name to overwrite the remote suggested file name.
        /// </summary>
        public string SuggestedFileName { get; set; }

        internal string LocalFileFullPath { get; set; }

        internal string DownloadInfoFileFullPath { get; set; }

        /// <summary>
        /// 最大线程数
        /// </summary>
        private const int MaxThreadCount = 30;


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
                if (DownloadInfo.ServerFileInfo == null || !DownloadInfo.ServerFileInfo.IsResumable || DownloadInfo.ServerFileInfo.Size < 1024 * 1024 * 1024)
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
        protected virtual async Task<bool> LoadDownloadInfoAsync(bool LoadSrc = true)
        {
            try
            {
                if (File.Exists(DownloadInfoFileFullPath))
                {
                    var downloadInfoText = File.ReadAllText(DownloadInfoFileFullPath);
                    var downloadInfo = JsonConvert.DeserializeObject<DownloadInfo>(downloadInfoText);

                    var count = downloadInfo.Count(m => m.Size == 0);

                    Console.WriteLine("downloadInfo" + count);

                    if (LoadSrc)
                    {
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
                    }
                    DownloadInfo.AddRange(downloadInfo);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
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
        private async Task<bool> CompleteAsync(bool isForce = false)
        {
            try
            {
                if (isForce || DownloadInfo.Percentage >= 100)
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


        public virtual async Task LoadAsync()
        {
            //检查参数
            CheckArgument();
            //获取Server文件信息
            DownloadInfo.ServerFileInfo = await ServerHelper.LoadServerInfoAsync(Url);
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
                if (File.Exists(LocalFileFullPath))
                {
                    using (var localFile = new FileStream(LocalFileFullPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                    {
                        if (localFile.Length >= DownloadInfo.ServerFileInfo.Size)
                        {
                            IsDownloaded = true;
                            await CompleteAsync(true);
                            return;
                        }
                    }
                }
                //拆分分片
                var fileSegments = await this.LoadFileSegmentsAsync();
                DownloadInfo.AddRange(fileSegments);
                await SaveDownloadInfoAsync();
            }

        }

        public bool IsDownloaded = false;


        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="progressAction"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DownloadFileAsync(Action<ProgressInfo> progressAction, CancellationToken cancellationToken = default)
        {
            if (IsDownloaded)
            {
                return;
            }

            SpeedCalculator speedCalculator = new SpeedCalculator();
            ProgressInfo progressInfo = new ProgressInfo();
            speedCalculator.Updated += async (h) =>
            {
                progressInfo.AverageSpeed = speedCalculator.AverageSpeed;
                progressInfo.CurrentValue = speedCalculator.CurrentValue;
                progressInfo.Speed = speedCalculator.Speed;
                progressInfo.Percentage = DownloadInfo.Percentage;
                progressInfo.TargetValue = DownloadInfo?.Size;
                progressAction.Invoke(progressInfo);
                var count = DownloadInfo.Count(m => m.Size == 0);
                Console.WriteLine("count " + count);
                await SaveDownloadInfoAsync();
                //if (float.IsNaN(progressInfo.Percentage))
                //{
                //    var count = DownloadInfo.Count(m => m.Size == 0);
                //    Console.WriteLine("count " + count);
                //    if (count == 0)
                //    {
                //        progressInfo.TargetValue = DownloadInfo.Size;
                //    }
                //    else
                //    {
                //        progressInfo.Percentage = 0;
                //    }
                //}
                //progressAction.Invoke(progressInfo);
                //if (progressInfo.Percentage >= 100)//|| (DownloadInfo.Size > 0 && DownloadInfo.Size <= DownloadInfo.TotalReadBytes)
                //{
                //    speedCalculator.Stop();
                //    await CompleteAsync(true);
                //}
            };

            foreach (var segment in DownloadInfo)
            {
                if (segment.TotalReadBytes != 0 && segment.TotalReadBytes >= segment.Size)
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
            //await FileSegmentaionTasks.StartAndWaitAllThrottled(MaxThreadCount);
            var errorCount = await CheckDownloadInfo(speedCalculator, cancellationToken);
            speedCalculator.Stop();
            if (errorCount == 0)
            {
                await ReconstructSegmentsAsync();
                await CompleteAsync(true);
            }

            //await Task.WhenAny(FileSegmentaionTasks);
        }

        /// <summary>
        /// 检查是否下载完成
        /// </summary>
        /// <param name="speedCalculator"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<int> CheckDownloadInfo(SpeedCalculator speedCalculator, CancellationToken cancellationToken)
        {
            var retryTasks = new List<Task>();
            var retryCount = 0;
            int errorCount = 0;
            while ((errorCount = DownloadInfo.Count(m => m.Size == 0 || m.TotalReadBytes == 0 || m.TotalReadBytes < m.Size ||
            (!string.IsNullOrEmpty(m.TempFile) && !File.Exists(m.TempFile)))) > 0)
            {
                retryCount++;

                if (retryCount > 3)
                {
                    break;
                }

                Console.WriteLine($"错误数据个数:{errorCount},开始第{retryCount}次重试");

                foreach (var item in DownloadInfo)
                {
                    if (item.Size == 0 || item.TotalReadBytes == 0 || item.TotalReadBytes < item.Size)
                    {
                        var task = DownloadSegmentFileAsync(item, (r, percentage) =>
                        {
                            speedCalculator.CurrentValue += r;
                        }, cancellationToken);
                        retryTasks.Add(task);
                    }
                }

                await Task.WhenAll(retryTasks);
                //await retryList.StartAndWaitAllThrottled(MaxThreadCount);
            }
            return errorCount;

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
            Stream localFile = null;
            if (string.IsNullOrEmpty(segment.TempFile))
            {
                localFile = new FileStream(LocalFileFullPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
                localFile.Position = segment.Start + segment.TotalReadBytes;
            }
            else
            {
                localFile = new FileStream(segment.TempFile, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            }
            segment.DstStream = localFile;
            var task = segment.DownloadAsync(progressAction, cancellationToken);
            return task;
        }

        protected virtual Task ReconstructSegmentsAsync()
        {
            return Task.CompletedTask;
        }

    }
}



