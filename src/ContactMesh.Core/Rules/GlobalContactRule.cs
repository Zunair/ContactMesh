// File: GlobalContactRule.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;

namespace ContactMesh.Core.Rules
{
    public sealed class GlobalContactRule
    {
        public IReadOnlyList<MeshContact> SelectContacts(IEnumerable<MeshContact> contacts)
        {
            return contacts.ToList();
        }
    }
}
