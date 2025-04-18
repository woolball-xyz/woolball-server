using Domain.Entities;
using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class ApplicationUserRepository(ApplicationDbContext context)
    : GenericRepository<ApplicationUser>(context),
        IApplicationUserRepository
{
    public async Task<decimal> GetInputBalanceByTokenAsync(Guid token)
    {
        return await DbContext
            .ApplicationUsers.Where(e => e.Token == token.ToString())
            .Select(e => e.InputBalance)
            .FirstOrDefaultAsync();
    }
}
