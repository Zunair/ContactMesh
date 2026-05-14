namespace ContactMesh.Core.Merge;

public sealed class EmailNormalizer
{
    public string NormalizeForComparison(string? email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();
    }
}
