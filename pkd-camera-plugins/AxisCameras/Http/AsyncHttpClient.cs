using System.Net;
using pkd_common_utils.Logging;

namespace AxisCameras.Http;

public class AsyncHttpClient : IDisposable
{
    private readonly HttpClient _client = new();
    private bool _disposed;
    
    ~AsyncHttpClient() => Dispose(false);

    public Func<AsyncHttpClient, Task>? RequestOkCallback { get; set; }
    public Func<AsyncHttpClient, string, Task>? RequestFailedCallback { get; set; }
    public Action<AsyncHttpClient, string>? RequestTimeoutCallback { get; set; }

    public string BaseUrl
    {
        get => _client.BaseAddress?.ToString() ?? string.Empty;
        set => _client.BaseAddress = new Uri(value);
    }
    
    public void CancelPendingRequests() => _client.CancelPendingRequests();
    
    public async Task SendGetAsync(string url, string username, string password)
    {
        var request = HttpResponseFactory.CreateGetRequest(url);
        var response = await _client.SendAsync(request);
        
        Logger.Debug($"AsyncHttpClient.SendGetAsync() - auth headers: {response.Headers.WwwAuthenticate}");

        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                RequestOkCallback?.Invoke(this);
                break;
            case HttpStatusCode.Unauthorized:
                await SendAuthGet(response, url, username, password);
                break;
            case HttpStatusCode.GatewayTimeout:
            case HttpStatusCode.RequestTimeout:
                RequestTimeoutCallback?.Invoke(this, $"Request timed out for {url}");
                break;
            default:
                Logger.Error($"AsyncHttpClient.SendGetAsync for device {_client.BaseAddress} failed: {response.ReasonPhrase}");
                break;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _client.CancelPendingRequests();
            _client.Dispose();
        }
        _disposed = true;
    }

    private async Task SendAuthGet(HttpResponseMessage response, string url, string username, string password)
    {
        try
        {
            var authData = HttpResponseFactory.GetWwwAuthentication(response);
            authData.Username = username;
            authData.Password = password;
            authData.ResponseNonce = new Random().Next(123400, 999999).ToString();
            var request = HttpResponseFactory.CreateGetRequest(url);
            request.Headers.Add("Authorization", HttpResponseFactory.CreateAuthHeader(authData, request));
            var authResponse = await _client.SendAsync(request);
            if (!authResponse.IsSuccessStatusCode)
            {
                RequestFailedCallback?.Invoke(this, authResponse.ReasonPhrase ?? "Unknown Error");
            }
            else
            {
                RequestOkCallback?.Invoke(this);
            }
        }
        catch (Exception e)
        {
            Logger.Error($"{e.Message} - {e.StackTrace?.Replace("\n","\n\r")}");
        }
    }
}