using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace FluentDownloader.Internal
{
    public interface IHttpClientFactory : IDisposable
    {
        HttpClient GetHttpClient(string url);
    }
}
