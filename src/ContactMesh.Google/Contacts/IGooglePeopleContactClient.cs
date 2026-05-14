namespace ContactMesh.Google.Contacts;

public interface IGooglePeopleContactClient
{
    Task<IReadOnlyList<GooglePersonContact>> ListAsync(string userId, CancellationToken cancellationToken);
    Task CreateAsync(string userId, GooglePersonContact contact, CancellationToken cancellationToken);
    Task UpdateAsync(string userId, GooglePersonContact contact, CancellationToken cancellationToken);
    Task DeleteAsync(string userId, string resourceName, CancellationToken cancellationToken);
}
