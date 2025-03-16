using MassTransit;

namespace Queue;

public class MassTransitMessagePublisher(IBusControl _busControl) : IMessagePublisher
{
    public async Task PublishAsync<T>(string queue, T message)
        where T : class
    {
        var sendEndpoint = await _busControl.GetSendEndpoint(new Uri($"queue:{queue}"));
        await sendEndpoint.Send(message);
    }
}
