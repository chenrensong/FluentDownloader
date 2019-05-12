using FluentDownloader.NetworkFile;
using System;
using System.IO;
using System.IO.Pipelines;
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
            await Task.WhenAll
                (
                    Task.Run(async () =>
                    {
                        int bytesRead;
                        try
                        {
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
                            throw ex;
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
                                pipeline.Reader.AdvanceTo(readResult.Buffer.End);
                                if (readResult.IsCompleted || readResult.IsCanceled)
                                {
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            throw ex;
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
