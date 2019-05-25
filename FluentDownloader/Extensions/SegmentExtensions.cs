using FluentDownloader.Internal;
using FluentDownloader.NetworkFile;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace FluentDownloader.Extensions
{
    public static class SegmentExtensions
    {
        public static DownloadTask DownloadAsync(this DownloadSegmentInfo downloadInfo,
         Action<long, float> progressAction, CancellationToken cancellationToken = default)
        {
            Pipe pipeline = new Pipe();
            bool isCompleted = false;
            var downloadTask = new DownloadTask(downloadInfo);
            Func<List<Task>> func = new Func<List<Task>>(() =>
            {
                var readTask = Task.Run(async () =>
                {
                    int bytesRead;
                    try
                    {
                        if (downloadInfo.Percentage >= 100)
                        {
                            return;
                        }
                        if (downloadInfo.Size != 0 && downloadInfo.TotalReadBytes == downloadInfo.Size)
                        {
                            return;
                        }
                        if (downloadInfo.SrcStream == null)
                        {
                            var httpClient = HttpClientFactory.Instance.GetHttpClient(downloadInfo.Url);
                            if (downloadInfo.Size != 0)
                            {
                                httpClient.DefaultRequestHeaders.Range = new RangeHeaderValue(downloadInfo.TotalReadBytes, downloadInfo.Size);
                            }
                            var response = await httpClient.GetAsync(downloadInfo.Url);
                            downloadInfo.SrcStream = await response.Content.ReadAsStreamAsync();
                            var size = response.Content.Headers.ContentLength.GetValueOrDefault();
                            if (size > 0 && downloadInfo.Size == 0)
                            {
                                downloadInfo.Size = size;
                                if (downloadInfo.End == 0)
                                {
                                    downloadInfo.End = downloadInfo.Start + size;
                                }
                            }
                            if (downloadInfo.TotalReadBytes > 0)
                            {
                                ///设置文件流写入位置
                                downloadInfo.DstStream.Position = downloadInfo.Start + downloadInfo.TotalReadBytes;
                            }
                        }

                        while (true) // Where the downloading part is happening
                        {
                            bytesRead = await downloadInfo.SrcStream.ReadAsync(pipeline.Writer.GetMemory(), cancellationToken);
                            if (bytesRead <= 0)
                            {
                                break;
                            }
                            pipeline.Writer.Advance(bytesRead);
                            var flushResult = await pipeline.Writer.FlushAsync(cancellationToken);
                            if (flushResult.IsCanceled)
                            {
                                break;
                            }
                            if (flushResult.IsCompleted)
                            {
                                break;
                            }
                        }
                        pipeline.Writer.Complete();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                    finally
                    {
                        isCompleted = true;
                    }
                }, cancellationToken);

                var writeTask = Task.Run(async () =>
                {
                    float percentage = 0;
                    long bytesRead = 0;
                    try
                    {
                        while (true)
                        {
                            var readResult = await pipeline.Reader.ReadAsync(cancellationToken);
                            foreach (var segment in readResult.Buffer)
                            {
                                bytesRead += segment.Length;
                                await downloadInfo.DstStream.WriteAsync(segment, cancellationToken);
                            }
                            downloadInfo.TotalReadBytes += bytesRead;
                            if (bytesRead > 0)//有进度才会提示
                            {
                                percentage = downloadInfo.Percentage;
                                progressAction.Invoke(bytesRead, percentage); // To Get the current percentage.
                                bytesRead = 0;
                            }
                            pipeline.Reader.AdvanceTo(readResult.Buffer.End);
                            if (readResult.IsCompleted || readResult.IsCanceled)
                            {
                                break;
                            }
                            if (isCompleted)
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                    finally
                    {
                        isCompleted = true;
                        downloadTask.Close();
                    }
                    pipeline.Reader.Complete();
                }, cancellationToken);

                return new List<Task>() { readTask, writeTask };
            });
            downloadTask.Create(func);
            return downloadTask;
        }
    }

    public class DownloadTask
    {
        private Func<List<Task>> _func = null;
        private DownloadSegmentInfo _downloadSegmentInfo = null;

        public DownloadTask(DownloadSegmentInfo downloadSegmentInfo)
        {
            _downloadSegmentInfo = downloadSegmentInfo;
        }

        public void Create(Func<List<Task>> func)
        {
            _func = func;
        }

        public Task StartAsync()
        {
            if (_func != null)
            {
                var taskList = _func.Invoke();
                return Task.WhenAll(taskList);
            }
            return Task.CompletedTask;
        }

        public void Close()
        {
            if (_downloadSegmentInfo.SrcStream != null)
            {
                _downloadSegmentInfo.SrcStream.Close();
                _downloadSegmentInfo.SrcStream.Dispose();
                _downloadSegmentInfo.SrcStream = null;
            }
            if (_downloadSegmentInfo.DstStream != null)
            {
                _downloadSegmentInfo.DstStream.Close();
                _downloadSegmentInfo.DstStream.Dispose();
                _downloadSegmentInfo.DstStream = null;
            }
        }

    }
}
