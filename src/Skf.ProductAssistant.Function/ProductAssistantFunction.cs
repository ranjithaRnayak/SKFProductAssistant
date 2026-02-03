using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Skf.ProductAssistant.Application.DTOs;
using Skf.ProductAssistant.Application.Orchestration;

namespace Skf.ProductAssistant.Function;

/// <summary>
/// Azure Function HTTP endpoint for the SKF Product Assistant.
/// Single entry point for all chat interactions.
/// </summary>
public sealed class ProductAssistantFunction
{
    private readonly OrchestratorService _orchestrator;
    private readonly ILogger<ProductAssistantFunction> _logger;

    public ProductAssistantFunction(
        OrchestratorService orchestrator,
        ILogger<ProductAssistantFunction> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Processes a chat request and returns the assistant's response.
    /// </summary>
    /// <remarks>
    /// POST /api/chat
    /// Body: { "message": "What is the bore diameter of 6205-2RS?", "conversationId": "optional-id" }
    /// Response: { "answer": "...", "conversationId": "...", "intent": "Question", ... }
    /// </remarks>
    [Function("chat")]
    public async Task<HttpResponseData> ChatAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "chat")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Chat request received");

        try
        {
            // Parse request
            var request = await req.ReadFromJsonAsync<ChatRequest>(cancellationToken);

            if (request is null || string.IsNullOrWhiteSpace(request.Message))
            {
                return await CreateErrorResponseAsync(
                    req,
                    HttpStatusCode.BadRequest,
                    "Request body must contain a 'message' field.");
            }

            // Process through orchestrator
            var response = await _orchestrator.ProcessAsync(request, cancellationToken);

            // Return success response
            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(response, cancellationToken);
            return httpResponse;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in request body");
            return await CreateErrorResponseAsync(
                req,
                HttpStatusCode.BadRequest,
                "Invalid JSON format in request body.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing chat request");
            return await CreateErrorResponseAsync(
                req,
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred. Please try again.");
        }
    }

    private static async Task<HttpResponseData> CreateErrorResponseAsync(
        HttpRequestData req,
        HttpStatusCode statusCode,
        string message)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new { error = message });
        return response;
    }
}
