using Domain.Entities;

namespace Infrastructure.Repositories
{
    public interface IApplicationUserRepository
    {
        Task<decimal> GetInputBalanceByTokenAsync(Guid token);
    }
}
