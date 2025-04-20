using System.Collections;
using System.Net.Mime;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Authlete.AppService.Demo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddHttpClient()
    .AddLogging()
    .AddAuthorization()
    .AddAuthentication(option =>
    {
        option.DefaultAuthenticateScheme = AppServiceAuthenticationExternalProviderDefaults.AuthenticationScheme;
        option.DefaultChallengeScheme = AppServiceAuthenticationExternalProviderDefaults.AuthenticationScheme;
    })
    .AddScheme<AppServiceAuthenticationOptions, AppServiceAuthenticationExternalProviderHandler>(
        AppServiceAuthenticationExternalProviderDefaults.AuthenticationScheme, options =>
        {
            options.responseStatusCode = StatusCodes.Status302Found;
            options.authorizationOpetionQueries = "resource=" + Environment.GetEnvironmentVariable("RESOURCE_IDENTIFIER");
            options.nameType = "preferred_username";
            options.roleType = ClaimsIdentity.DefaultRoleClaimType;
        }
    );

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();


var RESOURCE_IDENTIFIER = Environment.GetEnvironmentVariable("RESOURCE_IDENTIFIER");


app.MapGet("/", (HttpRequest req) =>
{
    var principal = req.HttpContext.User;
    if (principal.Identity?.IsAuthenticated == true)
    {
        return Results.Text($"<h1>Hello {principal.Identity.Name} ({principal.GetNameIdentifierId()})</h1>", MediaTypeNames.Text.Html);
    }
    else
    {
        return Results.Text($"<a href=\"/.auth/login/authlete?resource={RESOURCE_IDENTIFIER}\">Login</a>", MediaTypeNames.Text.Html);
    }
});

app.MapGet("/callApi", (HttpRequest req, IHttpClientFactory factory, [FromServices] ILogger<Program> log) =>
{
    var accessToken = req.Headers.TryGetValue("X-MS-TOKEN-AUTHLETE-ACCESS-TOKEN", out var token) ? token.ToString() : null;
    var httpClient = factory.CreateClient();

    using(StringContent json = new(JsonSerializer.Serialize(new { name = "Authlete" }), Encoding.UTF8, MediaTypeNames.Application.Json))
    {
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        // var res = httpClient.PostAsync(RESOURCE_IDENTIFIER, json);
        var res = httpClient.GetAsync(RESOURCE_IDENTIFIER);
        if (res.Result.IsSuccessStatusCode)
        {
            return Results.Ok(res.Result.Content.ReadAsStringAsync().Result);
        }
        else
        {
            log.LogError($"Error: {res.Result.StatusCode} {res.Result.Content.ReadAsStringAsync().Result}");
            foreach (var header in res.Result.Headers)
            {
                log.LogInformation($"{header.Key}: {header.Value}");
            }
            log.LogInformation(RESOURCE_IDENTIFIER);

            return Results.Text($"<a href=\"/.auth/login/authlete?resource={RESOURCE_IDENTIFIER}\">Login</a>", MediaTypeNames.Text.Html);
        }
    }
});

app.MapGet("/env", [Authorize]() =>
{
    var env = Environment.GetEnvironmentVariables();
    var sb = new StringBuilder();
    foreach (DictionaryEntry entry in env)
    {
        sb.AppendLine($"{entry.Key}: {entry.Value}");
    }
    return Results.Text(sb.ToString(), MediaTypeNames.Text.Plain);
});

app.MapGet("/headers", [Authorize](HttpRequest req) =>
{
    var headers = req.Headers;
    var sb = new StringBuilder();
    foreach (var header in headers)
    {
        sb.AppendLine($"{header.Key}: {header.Value}");
    }
    return Results.Text(sb.ToString(), MediaTypeNames.Text.Plain);
});

app.Run();
