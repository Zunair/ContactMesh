// File: GoogleContactGroupLabelPlan.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Google.Contacts
{
    public sealed record GoogleContactGroupLabelPlan(
        IReadOnlyList<string> LabelsToCreate,
        IReadOnlyList<GoogleContactGroupLabelUpdate> LabelsToUpdate,
        IReadOnlyList<GoogleContactGroupLabel> LabelsToDelete,
        IReadOnlyDictionary<string, string> ResourceNamesByLabel);

    public sealed record GoogleContactGroupLabelUpdate(GoogleContactGroupLabel ExistingLabel, string DesiredName);
}
