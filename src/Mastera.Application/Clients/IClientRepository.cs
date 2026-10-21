using Mastera.Domain.Clients;

namespace Mastera.Application.Clients;

public interface IClientRepository
{
    Task<Client?> FindByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken);

    Task<Client?> FindAsync(ClientId id, CancellationToken cancellationToken);

    void Add(Client client);
}
