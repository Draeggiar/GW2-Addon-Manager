using System.Net.Http;

namespace GW2_Addon_Manager.Backend;

public class HttpClientFactory : IHttpClientFactory
{
    public HttpClient Create()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Gw2 Addon Manager");
        return client;
    }
}