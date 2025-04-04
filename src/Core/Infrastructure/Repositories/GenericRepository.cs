using Domain.Primitives;
using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public abstract class GenericRepository<TEntity>
    where TEntity : Entity
{
    /// <summary>

    protected GenericRepository(ApplicationDbContext dbContext) => DbContext = dbContext;

    /// <summary>
    /// Gets the database context.
    /// </summary>
    protected ApplicationDbContext DbContext { get; }
}
