namespace Qirsh.ServiceDefaults;

/// <summary>
/// `dotnet Service.dll --probe http://localhost:port/health/ready` exits 0 when healthy. Lets compose health-check
/// the slim ASP.NET runtime image, which ships without curl or wget.
/// </summary>
public static class HealthProbe
{
    public static async Task RunIfRequestedAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args is not ["--probe", var url])
        {
            return;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            using var response = await http.GetAsync(new Uri(url));
            Environment.Exit(response.IsSuccessStatusCode ? 0 : 1);
        }
        catch (HttpRequestException)
        {
            Environment.Exit(1);
        }
        catch (TaskCanceledException)
        {
            Environment.Exit(1);
        }
    }
}
