using FluentDownloader.Networking;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentDownloader.Extensions;
using System.Threading;

namespace Demo
{
    class Program
    {
        static async Task Main(string[] args)
        {

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            //await HttpDown(stopwatch);
            await M3U8Down(stopwatch);

            Console.ReadLine();
        }

        private static async Task HttpDown(Stopwatch stopwatch)
        {
            Downloader downloader = new Downloader(
                "http://download.skycn.com/hao123-soft-online-bcs/soft/2017_02_22_BaiduPlayer5Setup_5.exe", "C:\\Test");
            await downloader.LoadAsync();
            await downloader.DownloadFileAsync((e) =>
            {
                //Console.WriteLine(e); // Current Progress

                Console.WriteLine($"Time:{e.Time} AverageSpeed:{e.AverageSpeed.SizeSuffix()} CurrentValue:{e.CurrentValue.SizeSuffix()} Speed:{e.Speed.SizeSuffix()} Percentage:{e.Percentage}");
                if (e.Percentage >= 100)
                {
                    stopwatch.Stop();
                    Console.WriteLine(stopwatch.ElapsedMilliseconds);
                }
            });
        }

        private static async Task M3U8Down(Stopwatch stopwatch)
        {
            //机动部队
            //https://iqiyi.qq-zuidazy.com/20190506/10110_005140a1/800k/hls/index.m3u8
            //https://iqiyi.qq-zuidazy.com/20190506/10109_da4bd19a/800k/hls/index.m3u8
            //https://iqiyi.qq-zuidazy.com/20190506/10108_b2bd8bb2/800k/hls/index.m3u8
            //https://iqiyi.qq-zuidazy.com/20190506/10107_8b4c62e3/800k/hls/index.m3u8
            //https://iqiyi.qq-zuidazy.com/20190506/10106_32bdc1be/800k/hls/index.m3u8
            //https://iqiyi.qq-zuidazy.com/20190506/10105_fbb1785b/800k/hls/index.m3u8
            //https://iqiyi.qq-zuidazy.com/20190506/10104_771a135b/800k/hls/index.m3u8
            //https://iqiyi.qq-zuidazy.com/20190506/10103_5703cff8/800k/hls/index.m3u8
            var m3u8downloader = new M3U8Downloader("https://bili.meijuzuida.com/20190513/14281_c9571896/800k/hls/index.m3u8", "C:\\Test");
            m3u8downloader.SuggestedFileName = "13.m3u8";
            await m3u8downloader.LoadAsync();
            await m3u8downloader.DownloadFileAsync((e) =>
            {
                //Console.WriteLine(e); // Current Progress

                Console.WriteLine($"Time:{e.Time} AverageSpeed:{e.AverageSpeed.SizeSuffix()} CurrentValue:{e.CurrentValue.SizeSuffix()} Speed:{e.Speed.SizeSuffix()} Percentage:{e.Percentage}");
                if (e.Percentage >= 100)
                {
                    stopwatch.Stop();
                    Console.WriteLine(stopwatch.ElapsedMilliseconds);
                }
            });
        }
    }
}
