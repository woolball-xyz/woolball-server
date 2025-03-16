using Application.Logic;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // var assembly = typeof(DependencyInjection).Assembly;
        // services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(assembly));

        // services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IPerfilBusinessLogic, PerfilBusinessLogic>();
        services.AddScoped<INotificationBusinessLogic, NotificationBusinessLogic>();
        services.AddScoped<IPerfilBusinessLogic, PerfilBusinessLogic>();
        services.AddScoped<ILiveBusinessLogic, LiveBusinessLogic>();
        services.AddScoped<ITimeSelectionBusinessLogic, TimeSelectionBusinessLogic>();
        services.AddScoped<IJoinTimeBusinessLogic, JoinTimeBusinessLogic>();
        services.AddScoped<IHelpBackstageBusinessLogic, HelpBackstageBusinessLogic>();
        services.AddScoped<ICommentBusinessLogic, CommentBusinessLogic>();
        // services.AddScoped<IChatBusinessLogic, ChatBusinessLogic>();
        services.AddScoped<IFollowBusinessLogic, FollowBusinessLogic>();
        services.AddScoped<ILikeBusinessLogic, LikeBusinessLogic>();
        services.AddScoped<IHelpResponseBusinessLogic, HelpResponseBusinessLogic>();
        return services;
    }
}
