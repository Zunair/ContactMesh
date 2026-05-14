namespace ContactMesh.Google.Contacts;

public sealed record GoogleContactGroupLabel(
    string? ResourceName,
    string Name,
    IReadOnlyDictionary<string, string> ClientData)
{
    public bool IsOwnedBy(string appId)
    {
        return ClientData.TryGetValue(GoogleContactGroupLabelReconciler.AppIdClientDataKey, out var owner)
            && string.Equals(owner, appId, StringComparison.Ordinal);
    }

    public string ManagedLabelName => ClientData.TryGetValue(
        GoogleContactGroupLabelReconciler.LabelNameClientDataKey,
        out var labelName)
            ? labelName
            : Name;
}
