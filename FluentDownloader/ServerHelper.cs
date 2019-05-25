using FluentDownloader.Extensions;
using FluentDownloader.Internal;
using FluentDownloader.NetworkFile;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace FluentDownloader
{
    class ServerHelper
    {
        /// <summary>
        /// 加载文件信息
        /// </summary>
        /// <returns></returns>
        public static async Task<ServerFileInfo> LoadServerInfoAsync(string url)
        {
            var httpClient = HttpClientFactory.Instance.GetHttpClient(url);
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            HttpResponseMessage httpResponseMessage = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (httpResponseMessage.IsSuccessStatusCode == false)
            {
                throw new Exception(httpResponseMessage.ReasonPhrase);
            }
            stopwatch.Stop();
            Console.WriteLine($"Get responseHeadersRead {stopwatch.ElapsedMilliseconds}");
            stopwatch.Restart();
            //var isResumable = await IsResumable(url);
            var contentLength = httpResponseMessage.Content.Headers.ContentLength;
            var isResumable = await IsResumable(url);//contentLength.HasValue && contentLength.Value > 0;
            stopwatch.Stop();
            Console.WriteLine($"Get isResumable {stopwatch.ElapsedMilliseconds}");
            stopwatch.Restart();
            var downloadContent = await httpResponseMessage.Content.ReadAsStreamAsync();
            stopwatch.Stop();
            Console.WriteLine($"Get downloadContent {stopwatch.ElapsedMilliseconds}");
            stopwatch.Restart();
            var serverFileInfo = new ServerFileInfo
            {
                Name = httpResponseMessage.Content.Headers?.ContentDisposition?.FileName ?? httpResponseMessage.RequestMessage.RequestUri.Segments.LastOrDefault(),
                MediaType = httpResponseMessage.Content.Headers.ContentType.MediaType,
                Size = httpResponseMessage.Content.Headers.ContentLength.GetValueOrDefault(),
                Extension = httpResponseMessage.Content.Headers.ContentType.MediaType.GetFileExtension(),
                IsResumable = isResumable,
                DownloadContent = downloadContent,
                TotalReadBytes = 0
            };
            stopwatch.Stop();
            Console.WriteLine($"Get serverFileInfo {stopwatch.ElapsedMilliseconds}");
            return serverFileInfo;
        }


        public static async Task<bool> IsResumable(string url)
        {
            try
            {
                var httpClient = HttpClientFactory.Instance.GetHttpClient(url);
                httpClient.DefaultRequestHeaders.Range = new RangeHeaderValue(1, 1);
                using (HttpResponseMessage Result = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    return Result.StatusCode == HttpStatusCode.PartialContent;
                }
            }
            catch
            {
                return false;
            }
        }

    }
}
