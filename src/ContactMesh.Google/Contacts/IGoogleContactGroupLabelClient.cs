// File: IGoogleContactGroupLabelClient.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Google.Contacts
{
    public interface IGoogleContactGroupLabelClient
    {
        Task<IReadOnlyList<GoogleContactGroupLabel>> ListAsync(string userId, CancellationToken cancellationToken);

        Task CreateAsync(string userId, string labelName, IReadOnlyDictionary<string, string> clientData, CancellationToken cancellationToken);

        Task UpdateAsync(string userId, string resourceName, string labelName, IReadOnlyDictionary<string, string> clientData, CancellationToken cancellationToken);

        Task DeleteAsync(string userId, string resourceName, CancellationToken cancellationToken);
    }
}
