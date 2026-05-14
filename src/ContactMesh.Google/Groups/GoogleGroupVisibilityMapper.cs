using ContactMesh.Core.Models;

namespace ContactMesh.Google.Groups;

public static class GoogleGroupVisibilityMapper
{
    public static MeshGroupVisibility FromGoogleSetting(string? value)
    {
        return value?.Trim().ToUpperInvariant() switch
        {
            "ALL_IN_DOMAIN_CAN_VIEW" => MeshGroupVisibility.Domain,
            "ALL_MEMBERS_CAN_VIEW" => MeshGroupVisibility.Members,
            _ => MeshGroupVisibility.Hidden
        };
    }
}
