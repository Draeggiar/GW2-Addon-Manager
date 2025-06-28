using System.Threading.Tasks;

namespace GW2_Addon_Manager;

public interface IUpdateHelper
{
    Task<string> DownloadStringFromGithubApiAsync(string url);
    Task DownloadFileFromGithubApiAsync(string url, string destPath);
    Task<dynamic> GitReleaseInfoAsync(string gitUrl);
}