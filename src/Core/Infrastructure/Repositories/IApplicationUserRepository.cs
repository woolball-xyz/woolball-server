using Domain.Entities;

namespace Infrastructure.Repositories
{
    public interface IApplicationUserRepository
    {
        bool InputBalanceGreaterThanZeroByTokenAsync(Guid token);
    }
}
