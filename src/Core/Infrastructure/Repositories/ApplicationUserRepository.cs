using Domain.Entities;
using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class ApplicationUserRepository(ApplicationDbContext context)
    : GenericRepository<ApplicationUser>(context),
        IApplicationUserRepository
{
    public async Task<int> GetInputBalanceByTokenAsync(Guid token)
    {
        return await DbContext
            .ApplicationUsers.Where(e => e.Token == token)
            .Select(e => e.InputBalance)
            .FirstOrDefaultAsync();
    }
}
