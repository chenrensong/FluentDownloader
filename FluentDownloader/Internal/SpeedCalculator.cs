using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;

namespace FluentDownloader.Internal
{
    /// <summary>
    /// 速度计算器
    /// </summary>
    public class SpeedCalculator : IDisposable
    {
        private ulong time = 0;
        private Timer timer;
        private long lastValue = 0;

        /// <summary>
        /// 速度信息已更新
        /// 调用时并非在原线程
        /// </summary>
        public event Action<SpeedCalculator> Updated;

        /// <summary>
        /// 当前值
        /// </summary>
        public long CurrentValue { get; set; } = 0;

        /// <summary>
        /// 刷新的时间间隔，以毫秒表示，默认300
        /// </summary>
        public float Interval { get; private set; } = 300;

        /// <summary>
        /// 当前速度
        /// </summary>
        public float Speed { get; private set; } = 0;

        /// <summary>
        /// 平均速度
        /// </summary>
        public float AverageSpeed { get; private set; } = 0;

        /// <summary>
        /// 是否刷新速度
        /// </summary>
        public bool IsEnabled { get; private set; } = false;

        public void Start()
        {
            IsEnabled = true;
            if (timer == null)
            {
                timer = new Timer(Interval);
                timer.AutoReset = true;
                timer.Elapsed += Timer_Elapsed;
            }
            timer.Start();
        }

        public void Stop()
        {
            IsEnabled = false;
            if (timer == null)
            {
                return;
            }
            timer.Stop();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            time++;
            Speed = (CurrentValue - lastValue) / Interval * 1000;
            AverageSpeed = (time == 0 ? 0 : ((AverageSpeed * (time - 1) + Speed) / time));
            lastValue = CurrentValue;
            Updated?.Invoke(this);
        }

        public void Dispose()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Close();
                timer.Dispose();
                timer = null;
            }
        }
    }
}
