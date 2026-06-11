// File: MicrosoftUserProvider.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Directory
{
    public sealed class MicrosoftUserProvider : IDirectoryProvider
    {
        private readonly IMicrosoftGraphDirectoryClient? client;

        public MicrosoftUserProvider(IMicrosoftGraphDirectoryClient? client = null)
        {
            this.client = client;
        }

        public async Task<IReadOnlyList<MeshUser>> GetUsersAsync(CancellationToken cancellationToken)
        {
            if (this.client is null)
            {
                return Array.Empty<MeshUser>();
            }

            var users = await this.client.ListUsersAsync(cancellationToken).ConfigureAwait(false);

            return users
                .Where(user => !string.IsNullOrWhiteSpace(user.Id)
                    && (!string.IsNullOrWhiteSpace(user.Mail)
                        || !string.IsNullOrWhiteSpace(user.UserPrincipalName)
                        || user.ProxyAddresses.Any(address => !string.IsNullOrWhiteSpace(address))))
                .Select(MicrosoftDirectoryMapper.ToMeshUser)
                .ToList();
        }
    }
}
