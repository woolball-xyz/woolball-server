using Domain.Contracts;
using Domain.Entities;

namespace Application.Logic;

public interface ITemplateBusinessLogic
{
    Task<Template> GetById(Guid id);
}
