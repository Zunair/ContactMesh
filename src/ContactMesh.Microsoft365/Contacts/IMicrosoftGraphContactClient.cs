namespace ContactMesh.Microsoft365.Contacts;

public interface IMicrosoftGraphContactClient
{
    Task<IReadOnlyList<MicrosoftGraphContact>> ListAsync(string userId, CancellationToken cancellationToken);
    Task CreateAsync(string userId, MicrosoftGraphContact contact, CancellationToken cancellationToken);
    Task UpdateAsync(string userId, MicrosoftGraphContact contact, CancellationToken cancellationToken);
    Task DeleteAsync(string userId, string contactId, CancellationToken cancellationToken);
}
