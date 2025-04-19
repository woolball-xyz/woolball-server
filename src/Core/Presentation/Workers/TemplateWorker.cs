using Application.Logic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Background;

public sealed class TemplateWorker(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private const int EXECUTION_INTERVAL = 60000 * 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                // do anything each 5 minutes

                await Task.Delay(EXECUTION_INTERVAL, stoppingToken);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
