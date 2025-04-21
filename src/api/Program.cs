using System.ComponentModel;
using api.Tools;
using ModelContextProtocol.Server;


var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddHttpContextAccessor()
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<EchoTool>()
    .WithTools<AuthleteApiTool>(); // Register the new tool

var app = builder.Build();

// app.MapGet("/env", [Authorize]() =>
// {
//     var env = Environment.GetEnvironmentVariables();
//     var sb = new StringBuilder();
//     foreach (DictionaryEntry entry in env)
//     {
//         sb.AppendLine($"{entry.Key}: {entry.Value}");
//     }
//     return Results.Text(sb.ToString(), MediaTypeNames.Text.Plain);
// });

// app.MapGet("/headers", [Authorize](HttpRequest req) =>
// {
//     var headers = req.Headers;
//     var sb = new StringBuilder();
//     foreach (var header in headers)
//     {
//         sb.AppendLine($"{header.Key}: {header.Value}");
//     }
//     return Results.Text(sb.ToString(), MediaTypeNames.Text.Plain);
// });

app.MapMcp();
app.Run();
