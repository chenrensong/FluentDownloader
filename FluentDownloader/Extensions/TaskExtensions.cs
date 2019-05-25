using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FluentDownloader.Extensions
{
    static class TaskExtensions
    {
        /// <summary>
        /// Starts the given tasks and waits for them to complete. This will run, at most, the specified number of tasks in parallel.
        /// <para>NOTE: If one of the given tasks has already been started, an exception will be thrown.</para>
        /// </summary>
        /// <param name="tasksToRun">The tasks to run.</param>
        /// <param name="maxTasksToRunInParallel">The maximum number of tasks to run in parallel.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public static Task StartAndWaitAllThrottled(this IEnumerable<DownloadTask> tasksToRun, int maxTasksToRunInParallel, CancellationToken cancellationToken = new CancellationToken())
        {
            return StartAndWaitAllThrottled(tasksToRun, maxTasksToRunInParallel, -1, cancellationToken);
        }

        /// <summary>
        /// Starts the given tasks and waits for them to complete. This will run, at most, the specified number of tasks in parallel.
        /// <para>NOTE: If one of the given tasks has already been started, an exception will be thrown.</para>
        /// </summary>
        /// <param name="tasksToRun">The tasks to run.</param>
        /// <param name="maxTasksToRunInParallel">The maximum number of tasks to run in parallel.</param>
        /// <param name="timeoutInMilliseconds">The maximum milliseconds we should allow the max tasks to run in parallel before allowing another task to start. Specify -1 to wait indefinitely.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
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
            try
            {
                lock (queues)
                {
                    downloadTask = queues.Dequeue();
                }
            }
            catch (Exception ex)
            {

            }
            return downloadTask;
        }


    }
}
