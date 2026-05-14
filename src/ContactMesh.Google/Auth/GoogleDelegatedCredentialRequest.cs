namespace ContactMesh.Google.Auth;

public sealed record GoogleDelegatedCredentialRequest(
    string ServiceAccountFile,
    string SubjectUserEmail,
    IReadOnlyList<string> Scopes);
