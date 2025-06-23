using System.Text;
using System.Text.Json;
using Application.Logic;
using Domain.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Presentation.Models;
using Microsoft.OpenApi.Models;

namespace Presentation.EndPoints;

public static class TasksEndPoints
{
    public static void AddTasksEndPoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/v1/")
            .WithTags("AI Tasks")
            .WithOpenApi();

        // Speech-to-Text endpoint
        group.MapPost("speech-recognition", HandleSpeechToText)
            .WithName("SpeechToText")
            .WithSummary("Convert audio to text using Whisper models")
            .WithDescription("Transcribe audio files to text using state-of-the-art speech recognition models. Supports various audio formats and languages.")
            .Accepts<SpeechToTextRequest>("multipart/form-data")
            .Produces<List<STTChunk>>(200)
            .Produces<object>(400)
            .RequireRateLimiting("fixed")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Speech Recognition (Speech-to-Text)",
                Description = "Convert audio files to text using Whisper models. Supports MP3, WAV, M4A and other audio formats.",
                Tags = new List<OpenApiTag> { new() { Name = "Speech Recognition" } }
            });

        // Text-to-Speech endpoint
        group.MapPost("text-to-speech", HandleTextToSpeech)
            .WithName("TextToSpeech")
            .WithSummary("Generate natural speech from text")
            .WithDescription("Convert text to natural-sounding speech using MMS or Kokoro models. Supports multiple languages and voices.")
            .Accepts<TextToSpeechRequest>("application/json")
            .Produces<List<TTSResponse>>(200)
            .Produces<object>(400)
            .RequireRateLimiting("fixed")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Text-to-Speech",
                Description = "Generate natural speech from text using MMS or Kokoro models. Supports multiple languages and premium voices.",
                Tags = new List<OpenApiTag> { new() { Name = "Text-to-Speech" } }
            });

        // Translation endpoint
        group.MapPost("translation", HandleTranslation)
            .WithName("Translation")
            .WithSummary("Translate between 200+ languages")
            .WithDescription("Translate text between over 200 languages using NLLB models with FLORES200 language codes.")
            .Accepts<TranslationRequest>("application/json")
            .Produces<TranslationResponse>(200)
            .Produces<object>(400)
            .RequireRateLimiting("fixed")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Translation",
                Description = "Translate text between 200+ languages using NLLB models. Uses FLORES200 format for language codes.",
                Tags = new List<OpenApiTag> { new() { Name = "Translation" } }
            });

        // Text Generation - Transformers.js endpoint
        group.MapPost("text-generation", HandleTextGeneration)
            .WithName("TextGeneration")
            .WithSummary("Generate text with language models")
            .WithDescription("Generate text using various providers: Transformers.js, WebLLM, or MediaPipe. Supports different model types and parameters.")
            .Accepts<TextGenerationBaseRequest>("application/json")
            .Produces<GenerationResponse>(200)
            .Produces<object>(400)
            .RequireRateLimiting("fixed")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Text Generation",
                Description = "Generate text using powerful language models. Supports Transformers.js, WebLLM, and MediaPipe providers with various model configurations. Use the 'provider' field to select which provider-specific fields to show.",
                Tags = new List<OpenApiTag> { new() { Name = "Text Generation" } }
            });

    }

    private static async Task HandleSpeechToText(
        HttpContext context,
        [FromServices] ITaskBusinessLogic logic,
        CancellationToken cancellationToken
    )
    {
        await HandleTaskInternalFromForm("speech-to-text", context, logic, cancellationToken);
    }

    public static async Task HandleTextToSpeech(
        [FromBody] TextToSpeechRequest request,
        HttpContext context,
        [FromServices] ITaskBusinessLogic logic,
        CancellationToken cancellationToken
    )
    {
        await HandleTaskInternal("text-to-speech", context, logic, request ,cancellationToken);
    }

    public static async Task HandleTranslation(
        [FromBody] TranslationRequest request,
        HttpContext context,
        [FromServices] ITaskBusinessLogic logic,
        CancellationToken cancellationToken
    )
    {
        await HandleTaskInternal("translation", context, logic, request ,cancellationToken);
    }

    public static async Task HandleTextGeneration(
        [FromBody] TextGenerationBaseRequest request,
        HttpContext context,
        [FromServices] ITaskBusinessLogic logic,
        CancellationToken cancellationToken
    )
    {
        await HandleTaskInternal("text-generation", context, logic, request, cancellationToken);
    }

    private static async Task HandleTaskInternalFromForm(
        string task,
        HttpContext context,
        ITaskBusinessLogic logic,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var request = await TaskRequest.CreateFromForm(context.Request.Form, task);
            await ProcessTaskRequest(request, context, logic, cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "internal error" }),
                cancellationToken
            );
        }
    }

    private static async Task HandleTaskInternal(
        string task,
        HttpContext context,
        ITaskBusinessLogic logic,
        BaseTaskRequest initialRequest,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var request = await TaskRequest.Create(initialRequest, task);
            await ProcessTaskRequest(request, context, logic, cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "internal error" }),
                cancellationToken
            );
        }
    }

    private static async Task ProcessTaskRequest(
        TaskRequest? request,
        HttpContext context,
        ITaskBusinessLogic logic,
        CancellationToken cancellationToken
    )
    {
        if (request == null)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Invalid request." }),
                cancellationToken
            );
            return;
        }

        var (result, error) = request.IsValidFields();
        if (!result)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error }), cancellationToken);
            return;
        }

        if (!await logic.PublishPreProcessingQueueAsync(request))
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(
                    new
                    {
                        error = "Unable to process request due to queue service unavailability",
                    }
                ),
                cancellationToken
            );
            return;
        }

        bool isStreaming =
            request.Kwargs.ContainsKey("stream")
            && request.Kwargs["stream"].ToString().ToLower() == "true";

        if (isStreaming)
        {
            context.Response.ContentType = "text/plain";

            await foreach (
                var message in logic.StreamTaskResultAsync(request, cancellationToken)
            )
            {
                var bytes = Encoding.UTF8.GetBytes(message + "\n");
                await context.Response.Body.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }

            context.Response.Body.Close();
        }
        else
        {
            var response = await logic.AwaitTaskResultAsync(request);
            if (!string.IsNullOrEmpty(response))
            {
                if (
                    response.Contains("\"Status\":\"Error\"")
                    || response.Contains("\"error\":")
                )
                {
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(response, cancellationToken);
                }
                else
                {
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(response, cancellationToken);
                }
            }
            else
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(
                        new { error = "Could not get response from service" }
                    ),
                    cancellationToken
                );
            }
        }
    }
}
