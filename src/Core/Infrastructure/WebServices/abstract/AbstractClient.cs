using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.WebServices;

public abstract class AbstractClient(IHttpClientFactory factory, string clientName) : IRetryClient
{
    const int RETRIES = 3;
    const int WAIT_FACTOR = 5000;

    public async Task<T> CallWithRetry<T>(Func<HttpClient, Task<T>> func)
    {
        using var client = factory.CreateClient(clientName);
        var remainRetries = RETRIES;

        while (remainRetries > 0)
        {
            try
            {
                var result = await func(client) ?? throw new Exception();
                return result;
            }
            catch (Exception)
            {
                await Task.Delay(WAIT_FACTOR / remainRetries);
                remainRetries--;
                if (remainRetries == 0)
                {
                    throw;
                }
            }
        }
        throw new InvalidOperationException();
    }

    public async Task<T> GetAsync<T>(string route)
    {
        return await CallWithRetry(
            async (client) =>
            {
                var response = await client.GetAsync(route);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>() ?? throw new Exception();
            }
        );
    }

    public async Task<byte[]> GetBytesAsync(string route)
    {
        return await CallWithRetry(
            async (client) =>
            {
                var response = await client.GetAsync(route);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync() ?? throw new Exception();
            }
        );
    }

    public async Task<string> GetAsync(string route)
    {
        return await CallWithRetry(
            async (client) =>
            {
                var response = await client.GetAsync(route);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
        );
    }

    public async Task<T> PostAsync<T>(string route, object? body)
    {
        return await CallWithRetry(
            async (client) =>
            {
                var content = JsonSerializer.Serialize(body);
                var parameters = new StringContent(content, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(route, parameters);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>() ?? throw new Exception();
            }
        );
    }

    public async Task PostAsync(string route, object? body)
    {
        await CallWithRetry(
            async (client) =>
            {
                var content = JsonSerializer.Serialize(body);
                var parameters = new StringContent(content, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(route, parameters);
                response.EnsureSuccessStatusCode();
                return Task.CompletedTask;
            }
        );
    }

    public async Task<T> PutAsync<T>(string route, object? body)
    {
        return await CallWithRetry(
            async (client) =>
            {
                var content = JsonSerializer.Serialize(body);
                var parameters = new StringContent(content, Encoding.UTF8, "application/json");

                var response = await client.PutAsync(route, parameters);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>() ?? throw new Exception();
            }
        );
    }

    public async Task PutAsync(string route, object? body)
    {
        await CallWithRetry(
            async (client) =>
            {
                var content = JsonSerializer.Serialize(body);
                var parameters = new StringContent(content, Encoding.UTF8, "application/json");

                var response = await client.PutAsync(route, parameters);
                response.EnsureSuccessStatusCode();
                return Task.CompletedTask;
            }
        );
    }

    public async Task PutAsync(string route, MultipartFormDataContent? parameters)
    {
        await CallWithRetry(
            async (client) =>
            {
                var response = await client.PutAsync(route, parameters);
                response.EnsureSuccessStatusCode();
                return Task.CompletedTask;
            }
        );
    }
}
