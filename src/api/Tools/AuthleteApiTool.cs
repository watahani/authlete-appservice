﻿﻿using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.OpenApi.Validations;
using Microsoft.OpenApi.Writers;
using ModelContextProtocol.Server;

namespace api.Tools;

public class AuthleteApiTool(IHttpContextAccessor contextAccessor)
{
    private OpenApiDocument? _openApiDoc;
    private string? _initializationError = null;

    private async Task Initialize()
    {
        if (_openApiDoc != null)
        {
            return; // Already initialized
        }
        try
        {
            // Construct the path to the YAML file in the output directory
            var yamlPath = "3.0.yaml";

            if (!File.Exists(yamlPath))
            {
                _initializationError = $"OpenAPI specification file not found at: {yamlPath}";
                return;
            }

            // Read the YAML file content
            var yamlContent = await File.ReadAllTextAsync(yamlPath);
            var settings = new OpenApiReaderSettings
            {
                RuleSet = ValidationRuleSet.GetEmptyRuleSet()  // ルールなし→検証しない
            };
            // Parse the OpenAPI document
            var reader = new OpenApiStringReader(settings);
            _openApiDoc = reader.Read(yamlContent, out var diagnostic);

            if (diagnostic.Errors.Any())
            {
                var errors = diagnostic.Errors.Select(e => $"{e.Message} ({e.Pointer})");
                _initializationError = $"Failed to parse OpenAPI specification. Details: {string.Join(", ", errors)}";
            }
        }
        catch (Exception ex)
        {
            // Log the exception details if logging is configured
            Console.Error.WriteLine($"Error in AuthleteApiTool.Initialize: {ex}");
            _initializationError = $"An unexpected error occurred during initialization: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Searches the Authlete OpenAPI specification for information based on a query.")]
    public async Task<string> Search([Description("The search query to find relevant API information. Accept only English")] string query)
    {
        await Initialize();
        if (_initializationError != null)
        {
            return JsonSerializer.Serialize(new { error = "Tool not initialized properly.", details = _initializationError });
        }

        if (_openApiDoc == null)
        {
             return JsonSerializer.Serialize(new { error = "Tool not initialized. Please run the Initialize tool first." });
        }

        try
        {
            // Implement actual search logic based on the 'query' parameter.
            var searchResults = new List<object>();
            var lowerQuery = query.ToLowerInvariant();

            // Search in Paths and Operations
            foreach (var path in _openApiDoc.Paths)
            {
                foreach (var operation in path.Value.Operations)
                {
                    var queries = lowerQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (queries.All(q => path.Key.ToLowerInvariant().Contains(q) || operation.Value.Summary?.ToLowerInvariant().Contains(q) == true || operation.Value.Description?.ToLowerInvariant().Contains(q) == true))
                    {
                        var languages = new List<string>();
                        if (operation.Value.Extensions.TryGetValue("x-code-samples", out var samplesExtension) && samplesExtension is Microsoft.OpenApi.Any.OpenApiArray samplesArray)
                        {
                            foreach (var sample in samplesArray)
                            {
                                if (sample is Microsoft.OpenApi.Any.OpenApiObject sampleObject && sampleObject.TryGetValue("lang", out var langExtension) && langExtension is Microsoft.OpenApi.Any.OpenApiString langString)
                                {
                                    languages.Add(langString.Value);
                                }
                            }
                        }

                        searchResults.Add(new
                        {
                            path = path.Key,
                            method = operation.Key.ToString(),
                            description = operation.Value.Description,
                            languages = languages
                        });
                    }
                }
            }

            return JsonSerializer.Serialize(searchResults);
        }
        catch (Exception ex)
        {
            // Log the exception details if logging is configured
            Console.Error.WriteLine($"Error in AuthleteApiTool.Search: {ex}");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred while processing the OpenAPI specification.", details = ex.Message });
        }
    }

    [McpServerTool]
    [Description("Show example code for the specified API path and language.")]
    public async Task<string> ShowExample([Description("The API path to show the example for.")] string path, [Description("The API method")] string method, [Description("The programming language for the example.")] string language)
    {
        await Initialize();
        if (_initializationError != null)
        {
            return JsonSerializer.Serialize(new { error = "Tool not initialized properly.", details = _initializationError });
        }

        if (_openApiDoc == null)
        {
            return JsonSerializer.Serialize(new { error = "Tool not initialized. Please run the Initialize tool first." });
        }

        try
        {
            // Implement actual example retrieval logic based on the 'path' and 'language' parameters.
            var examples = new List<object>();

            foreach (var apiPath in _openApiDoc.Paths)
            {
                if (apiPath.Key.Equals(path, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var operation in apiPath.Value.Operations)
                    {
                        if (!operation.Key.ToString().Equals(method, StringComparison.OrdinalIgnoreCase))
                        {
                            continue; // Skip if the method does not match
                        }
                        if (operation.Value.Extensions.TryGetValue("x-code-samples", out var samplesExtension) && samplesExtension is Microsoft.OpenApi.Any.OpenApiArray samplesArray)
                        {
                            foreach (var sample in samplesArray)
                            {
                                if (sample is Microsoft.OpenApi.Any.OpenApiObject sampleObject && sampleObject.TryGetValue("lang", out var langExtension) && langExtension is Microsoft.OpenApi.Any.OpenApiString langString && langString.Value.Equals(language, StringComparison.OrdinalIgnoreCase))
                                {
                                    sampleObject.TryGetValue("source", out var sourceExtension);
                                    if (sourceExtension is Microsoft.OpenApi.Any.OpenApiString sourceString)
                                    {
                                        examples.Add(new
                                        {
                                            path = apiPath.Key,
                                            method = operation.Key.ToString(),
                                            code = sourceString.Value,
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return JsonSerializer.Serialize(examples);
        }
        catch (Exception ex)
        {
            // Log the exception details if logging is configured
            Console.Error.WriteLine($"Error in AuthleteApiTool.ShowExample: {ex}");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred while processing the OpenAPI specification.", details = ex.Message });
        }
    }
    [McpServerTool]
    [Description("Get Service")]
    public async Task<string> GetService([Description("Service ID")] string id)
    {
        // https://github.com/modelcontextprotocol/csharp-sdk/issues/185
        // HttpContext は message エンドポイントではなく sse エンドポイントに対するリクエストコンテキストであることに注意
        var headers = contextAccessor.HttpContext?.Request.Headers;
        if (headers == null)
        {
            return JsonSerializer.Serialize(new { error = "Headers are missing." });
        }
        foreach (var header in headers)
        {
            Console.WriteLine($"{header.Key}: {header.Value}");
        }
        var accessToken = contextAccessor.HttpContext?.Request.Headers.TryGetValue("X-MS-TOKEN-AUTHLETE-ACCESS-TOKEN", out var token) == true ? token.ToString() : null;
        if (string.IsNullOrEmpty(accessToken))
        {
            return JsonSerializer.Serialize(new { error = "Access token is missing." });
        }
        return JsonSerializer.Serialize(new { id = id, description = "this is sample service" });
    }

    [McpServerTool]
    [Description("Get Client")]
    public async Task<string> GetClient([Description("Client ID")] string id)
    {
        // https://github.com/modelcontextprotocol/csharp-sdk/issues/185
        // HttpContext は message エンドポイントではなく sse エンドポイントに対するリクエストコンテキストであることに注意
        var headers = contextAccessor.HttpContext?.Request.Headers;
        if (headers == null)
        {
            return JsonSerializer.Serialize(new { error = "Headers are missing." });
        }
        foreach (var header in headers)
        {
            Console.WriteLine($"{header.Key}: {header.Value}");
        }
        var accessToken = contextAccessor.HttpContext?.Request.Headers.TryGetValue("X-MS-TOKEN-AUTHLETE-ACCESS-TOKEN", out var token) == true ? token.ToString() : null;
        if (string.IsNullOrEmpty(accessToken))
        {
            return JsonSerializer.Serialize(new { error = "Access token is missing." });
        }
        return JsonSerializer.Serialize(new { id = id, description = "this is sample service" });
    }
}
