using Microsoft.EntityFrameworkCore;
using Npgsql;
using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Application.Exceptions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Infrastructure.Persistence;

public sealed class UnitOfWork(AuthDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_users_Email",
        })
        {
            var email = ex.Entries.Select(e => e.Entity).OfType<User>().First().Email;
            throw new EmailAlreadyRegisteredException(email);
        }
    }
}
