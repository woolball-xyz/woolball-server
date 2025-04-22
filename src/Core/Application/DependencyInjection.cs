using Application.Logic;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ITaskBusinessLogic, TaskBusinessLogic>();
        services.AddScoped<ISpeechToTextLogic, SpeechToTextLogic>();
        return services;
    }
}
