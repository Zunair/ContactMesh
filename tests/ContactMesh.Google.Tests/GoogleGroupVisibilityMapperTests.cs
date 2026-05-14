using ContactMesh.Core.Models;
using ContactMesh.Google.Groups;
using Xunit;

namespace ContactMesh.Google.Tests;

public sealed class GoogleGroupVisibilityMapperTests
{
    [Theory]
    [InlineData("ALL_IN_DOMAIN_CAN_VIEW", MeshGroupVisibility.Domain)]
    [InlineData("ALL_MEMBERS_CAN_VIEW", MeshGroupVisibility.Members)]
    [InlineData("NONE_CAN_VIEW", MeshGroupVisibility.Hidden)]
    [InlineData(null, MeshGroupVisibility.Hidden)]
    public void FromGoogleSetting_Maps_Google_Group_Settings_To_Core_Visibility(string? googleValue, MeshGroupVisibility expected)
    {
        var visibility = GoogleGroupVisibilityMapper.FromGoogleSetting(googleValue);

        Assert.Equal(expected, visibility);
    }
}
