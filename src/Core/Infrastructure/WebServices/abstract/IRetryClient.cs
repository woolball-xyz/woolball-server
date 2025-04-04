namespace Domain.WebServices;

public interface IRetryClient
{
    Task<T> GetAsync<T>(string route);
    Task<string> GetAsync(string route);
    Task<T> PostAsync<T>(string route, object? body);
    Task PostAsync(string route, object? body);
    Task<T> PutAsync<T>(string route, object? body);
    Task PutAsync(string route, object? body);
    Task<byte[]> GetBytesAsync(string route);
}
