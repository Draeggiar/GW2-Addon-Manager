using System;
using System.Net;

namespace GW2_Addon_Manager.Dependencies.WebClient
{
    public interface IWebClient : IDisposable
    {
        WebHeaderCollection Headers { get; set; }
        string DownloadString(string address);
        void DownloadFile(string url, string destPath);
    }
}