using System.Security.Cryptography;
using System.Text.RegularExpressions;
using pkd_common_utils.Logging;

namespace AxisCameras.Http;

internal static class HttpResponseFactory
{
    public static DigestHeader GetWwwAuthentication(HttpResponseMessage rxMessage)
    {
        DigestHeader wwwChallenge = new();
        foreach (var item in rxMessage.Headers.WwwAuthenticate)
        {
            if (item.Parameter == null || !item.Scheme.Equals("Digest")) continue;

            var props = item.Parameter.Split(',');
            foreach (var prop in props)
            {
                const string regex = @"(?<param>\w+)=(?<value>""*[\w,=,/,+,-,\d]+""*)";
                //const string regex = @"(?<param>\w+)=(?<value>"".*""+)";
                var match = Regex.Match(prop, regex);
                if (match.Success)
                {
                    var hasParam = match.Groups.TryGetValue("param", out var param);
                    var hasValue = match.Groups.TryGetValue("value", out var value);
                    if (!hasParam || !hasValue) continue;

                    switch (param?.Value)
                    {
                        case "realm":
                            wwwChallenge.Realm = value?.Value.Trim('"') ?? string.Empty;
                            break;
                        case "nonce":
                            wwwChallenge.Nonce = value?.Value.Trim('"') ?? string.Empty;
                            break;
                        case "algorithm":
                            wwwChallenge.Algorithm = value?.Value.Trim('"') ?? string.Empty;
                            break;
                        case "qop":
                            wwwChallenge.Qop = value?.Value.Trim('"') ?? string.Empty;
                            break;
                        case "sd":
                            wwwChallenge.Sd = value?.Value.Trim('"') ?? string.Empty;
                            break;
                    }
                }
            }
        }
        
        return wwwChallenge;
    }

    public static HttpRequestMessage CreateGetRequest(string requestUri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add("Accept", "*/*");
        request.Headers.Add("User-Agent", "AxisCameraTesting");
        request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
        request.Headers.Add("Connection", "keep-alive");
        return request;
    }
    
    public static string CreateAuthHeader(DigestHeader authData, HttpRequestMessage request)
    {
        var loginHash = CreateMd5Hash($"{authData.Username}:{authData.Realm}:{authData.Password}");
        var methodHash = CreateMd5Hash($"{request.Method}:{request.RequestUri}");
        var digest =  CreateMd5Hash($"{loginHash}:{authData.Nonce}:{authData.NonceCount:00000000}:{authData.ResponseNonce}:{authData.Qop}:{methodHash}");


        var header =
            $"Digest username=\"{authData.Username}\", realm=\"{authData.Realm}\", " +
            $"nonce=\"{authData.Nonce}\", uri=\"{request.RequestUri?.ToString()}\", algorithm=\"{authData.Algorithm}\", " +
            $"qop={authData.Qop}, nc=00000001, cnonce=\"{authData.ResponseNonce}\", response=\"{digest}\"";

        return header;
    }
    
    private static string CreateMd5Hash(string input)
    {
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash =  MD5.HashData(inputBytes);
        return string.Concat(hash.Select(x => x.ToString("x2")));
    }
}
