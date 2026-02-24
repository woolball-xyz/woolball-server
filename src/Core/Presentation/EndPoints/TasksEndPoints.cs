using System.Text;
using System.Text.Json;
using Application.Logic;
using Domain.Contracts;
using Domain.Contracts.Task.TextGeneration;
using Domain.Contracts.Task.SpeechToText;
using Domain.Contracts.Task.TextToSpeech;
using Domain.Contracts.Task.Translation;
using Domain.Contracts.Task.ImageTextToText;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
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
            .Accepts<SpeechToTextRequestContract>("multipart/form-data")
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
        group.MapPost("text-to-speech", HandleTextToSpeechFromForm)
            .WithName("TextToSpeech")
            .WithSummary("Generate natural speech from text")
            .WithDescription("Convert text to natural-sounding speech using MMS or Kokoro models. Supports multiple languages and voices.")
            .Accepts<TextToSpeechRequestContract>("multipart/form-data")
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
        group.MapPost("translation", HandleTranslationFromForm)
            .WithName("Translation")
            .WithSummary("Translate between 200+ languages")
            .WithDescription("Translate text between over 200 languages using NLLB models with FLORES200 language codes.")
            .Accepts<TranslationRequestContract>("multipart/form-data")
            .Produces<TranslationResponse>(200)
            .Produces<object>(400)
            .RequireRateLimiting("fixed")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Translation",
                Description = "Translate text between 200+ languages using NLLB models. Uses FLORES200 format for language codes.",
                Tags = new List<OpenApiTag> { new() { Name = "Translation" } }
            });

        // Text Generation endpoint
        group.MapPost("text-generation", HandleTextGenerationFromForm)
            .WithName("TextGeneration")
            .WithSummary("Generate text with multiple AI providers")
            .WithDescription("Generate text using Transformers.js, WebLLM, or MediaPipe providers. Specify the provider field to choose which AI provider to use.")
            .Produces<TextGenerationResponse>(200)
            .Produces<object>(400)
            .RequireRateLimiting("fixed")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Text Generation - Multi-Provider",
                Description = "Generate text using multiple AI providers (Transformers.js, WebLLM, MediaPipe). Use the 'provider' field to specify which AI provider to use for text generation.",
                Tags = new List<OpenApiTag> { new() { Name = "Text Generation" } }
            });

        // Image-Text-to-Text endpoint
        group.MapPost("image-text-to-text", HandleImageTextToTextFromForm)
            .WithName("ImageTextToText")
            .WithSummary("Describe or answer questions about images")
            .WithDescription("Multimodal vision: send an image and a text prompt to get a text response. Input is a JSON string with image (base64) and text fields.")
            .Accepts<ImageTextToTextRequestContract>("multipart/form-data")
            .Produces<TextGenerationResponse>(200)
            .Produces<object>(400)
            .RequireRateLimiting("fixed")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Image-Text-to-Text (Vision)",
                Description = "Send an image and a text prompt to get a text answer. Input should be a JSON string: {\"image\":\"base64...\",\"text\":\"question\"}.",
                Tags = new List<OpenApiTag> { new() { Name = "Image-Text-to-Text" } }
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

    private static async Task HandleTextToSpeechFromForm(
        HttpContext context,
        [FromServices] ITaskBusinessLogic logic,
        CancellationToken cancellationToken
    )
    {
        await HandleTaskInternalFromForm("text-to-speech", context, logic, cancellationToken);
    }

    private static async Task HandleTranslationFromForm(
        HttpContext context,
        [FromServices] ITaskBusinessLogic logic,
        CancellationToken cancellationToken
    )
    {
        await HandleTaskInternalFromForm("translation", context, logic, cancellationToken);
    }

    private static async Task HandleTextGenerationFromForm(
        HttpContext context,
        [FromServices] ITaskBusinessLogic logic,
        CancellationToken cancellationToken
    )
    {
        await HandleTaskInternalFromForm("text-generation", context, logic, cancellationToken);
    }

    private static async Task HandleImageTextToTextFromForm(
        HttpContext context,
        [FromServices] ITaskBusinessLogic logic,
        CancellationToken cancellationToken
    )
    {
        await HandleTaskInternalFromForm("image-text-to-text", context, logic, cancellationToken);
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
            var request = await TaskRequestFactory.CreateFromForm(context.Request.Form, task);
            await ProcessTaskRequest(request, context, logic, cancellationToken);
        }
        catch (InvalidOperationException e)
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = e.Message }),
                cancellationToken
            );
        }
        catch (TimeoutException)
        {
            context.Response.StatusCode = 504;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Task processing timed out" }),
                CancellationToken.None
            );
        }
        catch (Exception e)
        {
            Console.WriteLine($"Request error: {e.GetType().Name}: {e.Message}");
            Console.WriteLine(e.StackTrace);
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
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
            context.Response.ContentType = "application/x-ndjson";
            context.Response.Headers["Cache-Control"] = "no-cache";

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
            var response = await logic.AwaitTaskResultAsync(request, cancellationToken);
            if (!string.IsNullOrEmpty(response))
            {
                bool isError = false;
                try
                {
                    using var doc = JsonDocument.Parse(response);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object
                        && doc.RootElement.TryGetProperty("Status", out var statusProp)
                        && statusProp.ValueKind == JsonValueKind.String)
                    {
                        var status = statusProp.GetString();
                        isError = status == "Error" || status == "failed";
                    }
                }
                catch (JsonException) { }

                context.Response.StatusCode = isError ? 500 : 200;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(response, cancellationToken);
            }
            else
            {
                context.Response.StatusCode = 504;
                context.Response.ContentType = "application/json";
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
