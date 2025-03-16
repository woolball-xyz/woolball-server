using Domain.Contracts;
using Domain.Entities;

using Infrastructure.Repositories;
using Queue;

namespace Application.Logic;

public sealed class TemplateBusinessLogic(
    ITemplateRepository templateRepository

) : ITemplateBusinessLogic
{
    public async Task<Template> GetById(Guid id) =>throw new NotImplementedException();

}
