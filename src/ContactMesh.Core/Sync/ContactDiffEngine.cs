// File: ContactDiffEngine.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync
{
    public sealed class ContactDiffEngine
    {
        private readonly SyncPlanner planner;

        public ContactDiffEngine(SyncPlanner? planner = null)
        {
            this.planner = planner ?? new SyncPlanner();
        }

        public ContactChangeSet Diff(IReadOnlyList<MeshContact> desiredContacts, IReadOnlyList<MeshContact> existingContacts)
        {
            return ContactChangeSet.FromOperations(this.planner.CreatePlan(desiredContacts, existingContacts));
        }
    }
}
