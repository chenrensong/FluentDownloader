using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace FluentDownloader.Internal
{
    public interface IHttpClient : IDisposable
    {
        HttpClient HttpClient { get; }

        HttpMessageHandler HttpMessageHandler { get; }

        string BaseUrl { get; set; }

        bool IsDisposed { get; }
    }
}
