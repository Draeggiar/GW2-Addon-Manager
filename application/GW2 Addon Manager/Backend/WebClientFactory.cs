using GW2_Addon_Manager.Dependencies.WebClient;

namespace GW2_Addon_Manager.Backend
{
    public static class WebClientFactory
    {
        public static WebClientWrapper Create()
        {
            var client = new WebClientWrapper();
            client.Headers.Add("User-Agent", "Gw2 Addon Manager");
            return client;
        }
    }
}