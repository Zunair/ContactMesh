using ContactMesh.Microsoft365.Contacts;
using Xunit;

namespace ContactMesh.Microsoft365.Tests;

public sealed class MicrosoftContactEmailSlotResetterTests
{
    [Fact]
    public async Task ResetAsync_Without_Apply_Reads_Matching_Contact_Only()
    {
        var client = new FakeContactClient(new[]
        {
            Contact("contact-1", "zftest@mhphope.org")
        });
        var resetter = new MicrosoftContactEmailSlotResetter(client);

        var result = await resetter.ResetAsync(
            "target@mhphope.org",
            "zftest@mhphope.org",
            "zftest@mhphope.org",
            apply: false,
            CancellationToken.None);

        Assert.False(result.Updated);
        Assert.Single(result.Matches);
        Assert.Empty(client.Updates);
    }

    [Fact]
    public async Task ResetAsync_With_Apply_Clears_Email_Slots_Then_Writes_Primary_Work_Email()
    {
        var client = new FakeContactClient(new[]
        {
            Contact(
                "contact-1",
                "zftest@mhphope.org",
                secondaryEmail: new MicrosoftGraphEmailAddress("old.other@mhphope.org", "ZF Test", "other"),
                genericEmail: new MicrosoftGraphEmailAddress("zftest@mhphope.org", "ZF Test", "other"))
        });
        var resetter = new MicrosoftContactEmailSlotResetter(client);

        var result = await resetter.ResetAsync(
            "target@mhphope.org",
            "zftest@mhphope.org",
            "zftest@mhphope.org",
            apply: true,
            CancellationToken.None);

        Assert.True(result.Updated);
        Assert.Equal(2, client.Updates.Count);

        Assert.Null(client.Updates[0].PrimaryEmailAddress);
        Assert.Null(client.Updates[0].SecondaryEmailAddress);
        Assert.Null(client.Updates[0].TertiaryEmailAddress);
        Assert.Empty(client.Updates[0].EmailAddresses);

        Assert.Equal("zftest@mhphope.org", client.Updates[1].PrimaryEmailAddress?.Address);
        Assert.Equal("work", client.Updates[1].PrimaryEmailAddress?.Type);
        Assert.Null(client.Updates[1].SecondaryEmailAddress);
        Assert.Null(client.Updates[1].TertiaryEmailAddress);
        Assert.Empty(client.Updates[1].EmailAddresses);
    }

    [Fact]
    public async Task ResetAsync_Refuses_To_Update_When_Multiple_Contacts_Match()
    {
        var client = new FakeContactClient(new[]
        {
            Contact("contact-1", "zftest@mhphope.org"),
            Contact("contact-2", "zftest@mhphope.org")
        });
        var resetter = new MicrosoftContactEmailSlotResetter(client);

        var result = await resetter.ResetAsync(
            "target@mhphope.org",
            "zftest@mhphope.org",
            "zftest@mhphope.org",
            apply: true,
            CancellationToken.None);

        Assert.False(result.Updated);
        Assert.Equal(2, result.Matches.Count);
        Assert.Empty(client.Updates);
    }

    [Fact]
    public async Task ResetByIdAsync_Can_Restore_Contact_When_Email_Slots_Are_Empty()
    {
        var client = new FakeContactClient(new[]
        {
            new MicrosoftGraphContact
            {
                Id = "contact-1",
                DisplayName = "ZF Test"
            }
        });
        var resetter = new MicrosoftContactEmailSlotResetter(client);

        var result = await resetter.ResetByIdAsync(
            "target@mhphope.org",
            "contact-1",
            "zftest@mhphope.org",
            apply: true,
            CancellationToken.None);

        Assert.True(result.Updated);
        Assert.Equal("zftest@mhphope.org", client.Updates[1].PrimaryEmailAddress?.Address);
        Assert.Null(client.Updates[1].SecondaryEmailAddress);
        Assert.Null(client.Updates[1].TertiaryEmailAddress);
    }

    private static MicrosoftGraphContact Contact(
        string id,
        string primaryEmail,
        MicrosoftGraphEmailAddress? secondaryEmail = null,
        MicrosoftGraphEmailAddress? genericEmail = null)
    {
        return new MicrosoftGraphContact
        {
            Id = id,
            DisplayName = "ZF Test",
            PrimaryEmailAddress = new MicrosoftGraphEmailAddress(primaryEmail, "ZF Test", "work"),
            SecondaryEmailAddress = secondaryEmail,
            EmailAddresses = genericEmail is null
                ? Array.Empty<MicrosoftGraphEmailAddress>()
                : new[] { genericEmail }
        };
    }

    private sealed class FakeContactClient : IMicrosoftGraphContactClient
    {
        private readonly IReadOnlyList<MicrosoftGraphContact> contacts;

        public FakeContactClient(IReadOnlyList<MicrosoftGraphContact> contacts)
        {
            this.contacts = contacts;
        }

        public List<MicrosoftGraphContact> Updates { get; } = new();

        public Task<IReadOnlyList<MicrosoftGraphContact>> ListAsync(string userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.contacts);
        }

        public Task CreateAsync(string userId, MicrosoftGraphContact contact, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(string userId, MicrosoftGraphContact contact, CancellationToken cancellationToken)
        {
            this.Updates.Add(contact);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            string userId,
            string contactId,
            string? contactFolderId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
