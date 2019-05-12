using FluentDownloader.Networking;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentDownloader.Extensions;

namespace Demo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            //Downloader downloader = new Downloader(
            //    "http://download.skycn.com/hao123-soft-online-bcs/soft/2017_02_22_BaiduPlayer5Setup_5.exe", "C:\\Test");
            //await downloader.LoadAsync();
            //await downloader.DownloadFileAsync((e) =>
            //{
            //    //Console.WriteLine(e); // Current Progress

            //    Console.WriteLine($"Time:{e.Time} AverageSpeed:{e.AverageSpeed.SizeSuffix()} CurrentValue:{e.CurrentValue.SizeSuffix()} Speed:{e.Speed.SizeSuffix()} Percentage:{e.Percentage}");
            //    if (e.Percentage >= 100)
            //    {
            //        stopwatch.Stop();
            //        Console.WriteLine(stopwatch.ElapsedMilliseconds);
            //    }
            //});



            var m3u8downloader = new M3U8Downloader("https://iqiyi.qq-zuidazy.com/20190506/10110_005140a1/800k/hls/index.m3u8", "C:\\Test");
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

            Console.ReadLine();
        }
    }
}
