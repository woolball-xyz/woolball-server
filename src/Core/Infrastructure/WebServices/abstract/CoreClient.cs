namespace Infrastructure.WebServices;

public sealed class CoreClient(IHttpClientFactory factory) : AbstractClient(factory, "CoreAPI") { }
