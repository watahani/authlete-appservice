using System.Collections;
using System.Net.Mime;
using System.Security.Claims;
using Authlete.AppService.Demo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using Azure.AI.OpenAI;
using Azure;
using Microsoft.Extensions.AI;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
string key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
var RESOURCE_IDENTIFIER = Environment.GetEnvironmentVariable("RESOURCE_IDENTIFIER");

if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
{
    Console.WriteLine("Please set the environment variables AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_API_KEY.");
    return;
}

AzureOpenAIClient azureOpenAIClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));

builder.Services
    .AddHttpClient()
    .AddSingleton(azureOpenAIClient)
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
            options.authorizationOpetionQueries = "resource=" + RESOURCE_IDENTIFIER;
            options.nameType = "preferred_username";
            options.roleType = ClaimsIdentity.DefaultRoleClaimType;
        }
    );

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment()){
    app.UseDeveloperExceptionPage();

}


app.MapGet("/", (HttpRequest req) =>
{
    var principal = req.HttpContext.User;
    if (principal.Identity?.IsAuthenticated == true)
    {
        return Results.Text($"<h1>Hello {principal.Identity.Name}</h1> <a href=\"/chat\">start chat</a><br/><a href=\"/.auth/logout?post_logout_redirect_uri=/\">Logout</a>", MediaTypeNames.Text.Html);
    }
    else
    {
        return Results.Text($"<a href=\"/startAuth\">Login</a>", MediaTypeNames.Text.Html);
    }
});

app.MapGet("/chat", [Authorize]
async () =>
{
    var htmlContent = await File.ReadAllTextAsync("chat/index.html");
    return Results.Content(htmlContent, "text/html");
});

app.MapGet("/startAuth",[Authorize] async (HttpRequest req) =>
{
    var referer = req.Headers["Referer"].ToString();
    var queries = req.Query;
    queries.Append(new ("state", referer));
    queries.Append(new ("resource", RESOURCE_IDENTIFIER));
    return Results.Redirect($"/.auth/login/authlete?{queries.ToString()}");
});

app.MapPost("/callApi", [Authorize] async ([FromBody] List<Microsoft.Extensions.AI.ChatMessage> messages, HttpRequest req, IHttpClientFactory factory, [FromServices] ILogger<Program> log, AzureOpenAIClient azureOpenAIClient) =>
{
    // debug messages
    foreach (var message in messages)
    {
        Console.WriteLine($"{message.Role}: {message.Text}");
    }
    var accessToken = req.Headers.TryGetValue("X-MS-TOKEN-AUTHLETE-ACCESS-TOKEN", out var token) ? token.ToString() : null;

    IClientTransport clientTransport = new SseClientTransport(new SseClientTransportOptions
    {
        Name = "Authlete",
        Endpoint = new Uri(RESOURCE_IDENTIFIER),
        AdditionalHeaders = new Dictionary<string, string>()
        {
           {"Authorization", $"Bearer {accessToken}"}
        }
    });

    var client = await McpClientFactory.CreateAsync(clientTransport);
    var mcpTools = await client.ListToolsAsync();

#pragma warning disable OPENAI001 // Suppress warning for evaluation-only API usage
    var chatClient = azureOpenAIClient.GetChatClient("gpt-4o");

    var res = await chatClient.AsIChatClient().AsBuilder().UseFunctionInvocation().Build()
                                .GetResponseAsync(messages, new ChatOptions
                                {
                                    ToolMode = ChatToolMode.Auto,
                                    Tools = [.. mcpTools]
                                });

    foreach (var message in res.Messages)
    {
        Console.WriteLine($"{message.Role}: {message.Text}");
        //show json response
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(message));
    }
    return Results.Ok(res.Messages);
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
