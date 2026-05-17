using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;

namespace Workshop.Application.Features.Customers;

public record DeleteCustomerCommand(Guid Id) : IRequest;

public class DeleteCustomerHandler : IRequestHandler<DeleteCustomerCommand>
{
    private readonly IWorkshopDbContext _db;
    public DeleteCustomerHandler(IWorkshopDbContext db) => _db = db;

    public async Task Handle(DeleteCustomerCommand cmd, CancellationToken ct)
    {
        var entity = await _db.Customers.FirstOrDefaultAsync(c => c.Id == cmd.Id && !c.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Customer {cmd.Id} not found");

        // Audit interceptor converts hard-delete to soft-delete via ISoftDeletable;
        // Customer base inherits IsDeleted, but the interceptor only triggers on EntityState.Deleted.
        entity.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }
}
