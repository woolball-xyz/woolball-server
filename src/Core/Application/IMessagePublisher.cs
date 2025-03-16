namespace Queue;

public interface IMessagePublisher
{
    Task PublishAsync<T>(string queue, T message)
        where T : class;
}
