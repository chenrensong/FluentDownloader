using FluentDownloader.Internal;
using FluentDownloader.NetworkFile;
using System;
using System.IO;
using System.IO.Pipelines;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace FluentDownloader.Extensions
{
    public static class SegmentExtensions
    {
        public static async Task DownloadAsync(this DownloadSegmentInfo downloadInfo,
         Action<long, float> progressAction, CancellationToken cancellationToken = default)
        {
            Pipe pipeline = new Pipe();
            bool isCompleted = false;
            await Task.WhenAll
                (
                    Task.Run(async () =>
                    {
                        int bytesRead;
                        try
                        {
                            if (downloadInfo.Size != 0 && downloadInfo.TotalReadBytes == downloadInfo.Size)
                            {
                                isCompleted = true;
                                return;
                            }
                            if (downloadInfo.SrcStream == null)
                            {
                                var httpClient = HttpClientFactory.Instance.GetHttpClient(downloadInfo.Url);
                                if (downloadInfo.Size != 0 && (downloadInfo.Start > 0 || downloadInfo.End > 0))
                                {
                                    httpClient.DefaultRequestHeaders.Range = new RangeHeaderValue(downloadInfo.Start + downloadInfo.TotalReadBytes, downloadInfo.End);
                                }
                                var response = await httpClient.GetAsync(downloadInfo.Url);
                                downloadInfo.SrcStream = await response.Content.ReadAsStreamAsync();
                                var size = response.Content.Headers.ContentLength.GetValueOrDefault();
                                if (downloadInfo.Size == 0)
                                {
                                    downloadInfo.Size = size;
                                    downloadInfo.End = size;
                                }
                                if (downloadInfo.TotalReadBytes > 0)
                                {
                                    ///设置文件流写入位置
                                    if (downloadInfo.DstStream != null)
                                    {
                                        downloadInfo.DstStream.Position = downloadInfo.Start + downloadInfo.TotalReadBytes;
                                    }
                                }
                            }
                            var srcStream = downloadInfo.SrcStream;
                            while (true) // Where the downloading part is happening
                            {
                                bytesRead = await srcStream.ReadAsync(pipeline.Writer.GetMemory(), cancellationToken);
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
                            isCompleted = true;
                        }
                        catch (Exception ex)
                        {
                            isCompleted = true;
                            Console.WriteLine("DownloadAsync Write " + ex.GetType().Name + ex.Message);
                        }
                    }, cancellationToken),

                    Task.Run(async () =>
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
                                else
                                {
                                    Console.WriteLine("empty" + bytesRead);
                                    //break;
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
                            pipeline.Reader.Complete();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("DownloadAsync Read " + ex.GetType().Name + ex.Message);
                        }
                        finally
                        {
                            if (downloadInfo.DstStream != null)
                            {
                                downloadInfo.DstStream.Close();
                                downloadInfo.DstStream.Dispose();
                            }
                        }
                    }, cancellationToken)
                );
        }
    }
}
