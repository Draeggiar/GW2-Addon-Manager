using System.Net.Http;

namespace GW2_Addon_Manager.Backend;

public interface IHttpClientFactory
{
    HttpClient Create();
}