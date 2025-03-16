using Domain.Contracts;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Infrastructure.Repositories;
using Queue;

namespace Application.Logic;

public sealed class TemplateBusinessLogic(
    ITemplateRepository templateRepository,

) : ITemplateBusinessLogic
{
    public async Task<Template> GetById(Guid id) => await templateRepository.GetById(id);

}
