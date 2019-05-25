using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FluentDownloader.Extensions
{
    static class DownloadTaskExtensions
    {
        public static Task StartAndWaitAllThrottled(this IEnumerable<DownloadTask> tasksToRun, int maxTasksToRunInParallel, CancellationToken cancellationToken = new CancellationToken())
        {
            return StartAndWaitAllThrottled(tasksToRun, maxTasksToRunInParallel, -1, cancellationToken);
        }

        public static Task StartAndWaitAllThrottled(this IEnumerable<DownloadTask> tasksToRun, int maxTasksToRunInParallel, int timeoutInMilliseconds, CancellationToken cancellationToken = new CancellationToken())
        {
            Queue<DownloadTask> queues = new Queue<DownloadTask>(tasksToRun);
            var postTaskTasks = new List<Task>();
            var threadCount = Math.Min(maxTasksToRunInParallel, queues.Count);
            for (int i = 0; i < threadCount; i++)
            {
                var task = StartNextTask(queues);
                postTaskTasks.Add(task);
            }
            return Task.WhenAll(postTaskTasks);
        }

        private static Task StartNextTask(Queue<DownloadTask> queues)
        {
            if (queues.Count <= 0)
            {
                return null;
            }
            return Task.Run(async () =>
            {
                DownloadTask downloadTask = SafeDequeue(queues);
                if (downloadTask != null)
                {
                    await downloadTask.StartAsync();
                }
                while (queues.Count > 0)
                {
                    DownloadTask nextTask = SafeDequeue(queues);
                    if (nextTask != null)
                    {
                        await nextTask.StartAsync();
                    }
                }
            });
        }

        private static DownloadTask SafeDequeue(Queue<DownloadTask> queues)
        {
            DownloadTask downloadTask = null;
            lock (queues)
            {
                try
                {
                    downloadTask = queues.Dequeue();
                }
                catch (Exception ex)
                {
                    //ignore
                }
            }
            return downloadTask;
        }


    }
}
