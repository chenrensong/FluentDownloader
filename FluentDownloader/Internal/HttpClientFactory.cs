using System;

namespace FluentDownloader.Internal
{
    internal class HttpClientFactory : HttpClientFactoryBase
    {
        public static HttpClientFactory Instance = new HttpClientFactory();

        private static int count = 0;
        /// <summary>
        /// 不允许实例化
        /// </summary>
        private HttpClientFactory()
        {

        }

        protected override string GetCacheKey(string url)
        {
            return url;
            //return new Uri(url).Host;
        }
    }
}
