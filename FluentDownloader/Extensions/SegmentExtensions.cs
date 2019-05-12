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
                            if (downloadInfo.Percentage >= 100)
                            {
                                isCompleted = true;
                                return;
                            }
                            if (downloadInfo.Size != 0 && downloadInfo.TotalReadBytes == downloadInfo.Size)
                            {
                                isCompleted = true;
                                return;
                            }
                            if (downloadInfo.SrcStream == null)
                            {
                                using (HttpClient httpClient = new HttpClient())
                                {
                                    if (downloadInfo.Size != 0)
                                    {
                                        httpClient.DefaultRequestHeaders.Range = new RangeHeaderValue(downloadInfo.TotalReadBytes, downloadInfo.Size);
                                    }
                                    //httpClient.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/36.0.1985.143 Safari/537.36");
                                    var response = await httpClient.GetAsync(downloadInfo.Url);
                                    downloadInfo.SrcStream = await response.Content.ReadAsStreamAsync();
                                    downloadInfo.Size = response.Content.Headers.ContentLength.GetValueOrDefault();
                                    if (downloadInfo.TotalReadBytes > 0)
                                    {
                                        ///设置文件流写入位置
                                        downloadInfo.DstStream.Position = downloadInfo.TotalReadBytes;
                                    }
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
                            isCompleted = true;
                        }
                        catch (Exception ex)
                        {
                            isCompleted = true;
                            Console.WriteLine(ex.Message);
                            //pipeline.Writer.Complete();
                            //throw ex;
                        }
                    }, cancellationToken),

                    Task.Run(async () =>
                    {
                        float percentage = 0;
                        long bytesRead = 0;
                        try
                        {
                            bool isFirst = true;
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
                                    progressAction.Invoke(isFirst ? downloadInfo.TotalReadBytes : bytesRead, percentage); // To Get the current percentage.
                                    bytesRead = 0;
                                }
                                isFirst = false;
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
                            //throw ex;
                        }
                        finally
                        {
                            if (downloadInfo.DstStream != null)
                            {
                                downloadInfo.DstStream.Close();
                                downloadInfo.DstStream.Dispose();
                            }
                        }
                        pipeline.Reader.Complete();
                    }, cancellationToken)
                );
        }
    }
}
