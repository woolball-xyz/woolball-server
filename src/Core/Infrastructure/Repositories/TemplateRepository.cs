using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class TemplateRepository(ApplicationDbContext context)
    : GenericRepository<Template>(context),
        ITemplateRepository
{
    // public async Task<TimeSelection> GetById(Guid id) =>
    //     await DbContext.TimeSelections.FirstOrDefaultAsync(e => e.Id == id)
    //     ?? throw new KeyNotFoundException("TimeSelection não encontrado.");
}
