using Microsoft.AspNetCore.Http;

namespace PkdAvfRestApi;

using System.Net;
using System.Text;
using System.Security.Claims;

internal class BasicAuthMiddleware(RequestDelegate next)
{
    private const string Realm = "RestBasicAuth";

    public async Task Invoke(HttpContext context)
    {
        string? authHeader = context.Request.Headers["Authorization"];

        
        if (authHeader != null && authHeader.StartsWith("Basic "))
        {
            var encodedUsernamePassword = authHeader.Substring("Basic ".Length).Trim();
            var decodedUsernamePassword = Encoding.UTF8.GetString(Convert.FromBase64String(encodedUsernamePassword));
            var parts = decodedUsernamePassword.Split(':');

            var username = parts[0];
            var password = parts[1];

            if (IsAuthorized(username, password))
            {
                var claims = new[] { new Claim(ClaimTypes.Name, username) };
                var identity = new ClaimsIdentity(claims, "Basic");
                context.User = new ClaimsPrincipal(identity);

                await next(context);
                return;
            }
        }

        context.Response.Headers["WWW-Authenticate"] = $"Basic realm=\"{Realm}\"";
        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;

    }
    
    private bool IsAuthorized(string username, string password)
    {
        // todo: replace this with actual user validation
        return username == "user" && password == "test123";
    }
}