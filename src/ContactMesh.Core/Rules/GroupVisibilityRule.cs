using ContactMesh.Core.Models;

namespace ContactMesh.Core.Rules;

public sealed class GroupVisibilityRule
{
    public bool IsVisibleToTarget(MeshContact contact, SyncTarget target)
    {
        return contact.Labels.Count == 0 || contact.Labels.Overlaps(target.LabelNames);
    }
}
