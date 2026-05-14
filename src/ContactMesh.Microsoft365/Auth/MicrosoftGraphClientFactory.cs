namespace ContactMesh.Microsoft365.Auth;

public sealed class MicrosoftGraphClientFactory
{
    private readonly Microsoft365Options options;

    public MicrosoftGraphClientFactory(Microsoft365Options options)
    {
        this.options = options;
    }

    public string? GetTenantId()
    {
        return this.options.TenantId;
    }
}
