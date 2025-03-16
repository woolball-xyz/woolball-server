using System.Net.Http.Headers;
using Domain.Contracts;
using Domain.Entities;
using Domain.WebServices;
using Infrastructure.WebServices;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.WebServices;

public sealed class TemplateWebService(CoreClient client) : ITemplateWebService
{
    const string baseRoute = "api/template";

    // public async Task<Template> GetTemplate(Guid id)
    // {
    //     var route = Path.Combine(baseRoute, string.Empty);
    //     return await client.GetAsync<string>(route);
    // }

}
