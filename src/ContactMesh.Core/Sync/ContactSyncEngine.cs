// File: ContactSyncEngine.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync
{
    public sealed class ContactSyncEngine
    {
        private readonly IContactProvider contactProvider;
        private readonly SyncPlanner planner;
        private readonly SyncExecutor executor;

        public ContactSyncEngine(IContactProvider contactProvider, SyncPlanner? planner = null, SyncExecutor? executor = null)
        {
            this.contactProvider = contactProvider;
            this.planner = planner ?? new SyncPlanner();
            this.executor = executor ?? new SyncExecutor(contactProvider);
        }

        public async Task<SyncResult> SyncAsync(
            SyncTarget target,
            IReadOnlyList<MeshContact> desiredContacts,
            bool dryRun,
            CancellationToken cancellationToken,
            bool disableDeletes = false)
        {
            var existingContacts = await this.contactProvider.GetContactsAsync(target.UserId, cancellationToken).ConfigureAwait(false);
            var operations = this.planner.CreatePlan(desiredContacts, existingContacts);

            return await this.executor.ExecuteAsync(target, operations, dryRun, cancellationToken, disableDeletes).ConfigureAwait(false);
        }
    }
}
