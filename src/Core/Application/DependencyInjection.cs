using Application.Logic;
using Domain.Contracts;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ITaskBusinessLogic, TaskBusinessLogic>();
        services.AddScoped<ISpeechToTextLogic, SpeechToTextLogic>();
        services.AddScoped<ITextToSpeechLogic, TextToSpeechLogic>();
        services.AddScoped<ITextGenerationLogic, TextGenerationLogic>();
        services.AddScoped<ITranslationLogic, TranslationLogic>();
        services.AddSingleton<RedisChunkBuffer<STTChunk>>();
        services.AddSingleton<RedisChunkBuffer<TTSResponse>>();
        return services;
    }
}
