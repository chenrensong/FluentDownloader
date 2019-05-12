using System;
using System.Collections.Generic;
using System.Text;

namespace FluentDownloader.Networking
{
    public class ProgressInfo
    {
        /// <summary>
        /// 当前速度
        /// </summary>
        public float Speed;
        /// <summary>
        /// 百分比
        /// </summary>
        public float Percentage;
        /// <summary>
        /// 平均速度
        /// </summary>
        public float AverageSpeed;
        /// <summary>
        /// 当前进度值
        /// </summary>
        public long CurrentValue;
        /// <summary>
        /// 目标值
        /// </summary>
        public long? TargetValue;

        /// <summary>
        /// 剩余时间
        /// </summary>
        public long Time
        {
            get
            {
                var time = (long)(((long)TargetValue - CurrentValue) / (AverageSpeed + 0.001));
                return time;
            }
        }
    }
}
