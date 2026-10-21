using Mastera.Application.Clients;
using Mastera.Domain.Clients;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.Repositories;

public sealed class ClientRepository(MasteraDbContext db) : IClientRepository
{
    public Task<Client?> FindByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken) =>
        db.Clients.SingleOrDefaultAsync(c => c.Phone == phone, cancellationToken);

    public Task<Client?> FindAsync(ClientId id, CancellationToken cancellationToken) =>
        db.Clients.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Add(Client client) => db.Clients.Add(client);
}
