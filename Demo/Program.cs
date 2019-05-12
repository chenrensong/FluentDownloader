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

            Downloader downloader = new Downloader(
                "http://download.skycn.com/hao123-soft-online-bcs/soft/2017_02_22_BaiduPlayer5Setup_5.exe","C:\\Test");
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

            Console.ReadLine();
        }
    }
}
