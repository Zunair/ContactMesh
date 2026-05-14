using ContactMesh.Core.Models;
using ContactMesh.Google.Contacts;
using Xunit;

namespace ContactMesh.Google.Tests;

public sealed class GoogleContactBatchWriterTests
{
    [Fact]
    public async Task ApplyAsync_Reconciles_Labels_From_Created_And_Updated_Contacts()
    {
        var client = new FakeContactGroupLabelClient(
            new[]
            {
                Label("contactGroups/stale", "Stale", "contact-mesh", "Stale"),
                Label("contactGroups/directory", "directory", "contact-mesh", "directory"),
                Label("contactGroups/personal", "Personal", "other-app", "Personal")
            });
        var writer = new GoogleContactBatchWriter(labelClient: client);

        await writer.ApplyAsync(
            "user@example.org",
            new ContactChangeSet
            {
                Creates = new[] { Contact("Directory", "Sales") },
                Updates = new[] { Contact("Directory", "Engineering") },
                Deletes = new[] { Contact("Ignored") }
            },
            CancellationToken.None);

        Assert.Equal(
            new[]
            {
                "list:user@example.org",
                "delete:contactGroups/stale",
                "update:contactGroups/directory:Directory:contactmesh.appId=contact-mesh,contactmesh.labelName=Directory",
                "create:Sales:contactmesh.appId=contact-mesh,contactmesh.labelName=Sales",
                "create:Engineering:contactmesh.appId=contact-mesh,contactmesh.labelName=Engineering"
            },
            client.Calls);
    }

    [Fact]
    public async Task ApplyAsync_Skips_Label_Reconciliation_When_No_Label_Client_Is_Configured()
    {
        var writer = new GoogleContactBatchWriter();

        await writer.ApplyAsync(
            "user@example.org",
            new ContactChangeSet
            {
                Creates = new[] { Contact("Directory") }
            },
            CancellationToken.None);
    }

    [Fact]
    public async Task ApplyAsync_Writes_Create_Update_Delete_Contacts_When_Client_Is_Configured()
    {
        var client = new FakePeopleContactClient();
        var labelClient = new FakeContactGroupLabelClient(
            new[] { Label("contactGroups/directory", "Directory", "contact-mesh", "Directory") });
        var writer = new GoogleContactBatchWriter(contactClient: client, labelClient: labelClient);

        await writer.ApplyAsync(
            "user@example.org",
            new ContactChangeSet
            {
                Creates = new[] { Contact("Directory") with { SourceId = "directory-user-1" } },
                Updates = new[]
                {
                    Contact("Directory") with
                    {
                        SourceId = "directory-user-2",
                        Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            [GoogleContactMapper.ResourceNameMetadataKey] = "people/c456"
                        }
                    }
                },
                Deletes = new[]
                {
                    Contact("Directory") with
                    {
                        Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            [GoogleContactMapper.ResourceNameMetadataKey] = "people/c789"
                        }
                    }
                }
            },
            CancellationToken.None);

        Assert.Equal(
            new[]
            {
                "create:user@example.org:directory-user-1::contactGroups/directory",
                "update:user@example.org:directory-user-2:people/c456:contactGroups/directory",
                "delete:user@example.org:people/c789"
            },
            client.Calls);
    }

    private static MeshContact Contact(params string[] labels)
    {
        return new MeshContact
        {
            Labels = labels.ToHashSet(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static GoogleContactGroupLabel Label(
        string resourceName,
        string name,
        string appId,
        string labelName)
    {
        return new GoogleContactGroupLabel(
            resourceName,
            name,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GoogleContactGroupLabelReconciler.AppIdClientDataKey] = appId,
                [GoogleContactGroupLabelReconciler.LabelNameClientDataKey] = labelName
            });
    }

    private sealed class FakeContactGroupLabelClient : IGoogleContactGroupLabelClient
    {
        private readonly IReadOnlyList<GoogleContactGroupLabel> labels;

        public FakeContactGroupLabelClient(IReadOnlyList<GoogleContactGroupLabel> labels)
        {
            this.labels = labels;
        }

        public List<string> Calls { get; } = new();

        public Task<IReadOnlyList<GoogleContactGroupLabel>> ListAsync(string userId, CancellationToken cancellationToken)
        {
            this.Calls.Add($"list:{userId}");

            return Task.FromResult(this.labels);
        }

        public Task CreateAsync(
            string userId,
            string labelName,
            IReadOnlyDictionary<string, string> clientData,
            CancellationToken cancellationToken)
        {
            this.Calls.Add($"create:{labelName}:{FormatClientData(clientData)}");

            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            string userId,
            string resourceName,
            string labelName,
            IReadOnlyDictionary<string, string> clientData,
            CancellationToken cancellationToken)
        {
            this.Calls.Add($"update:{resourceName}:{labelName}:{FormatClientData(clientData)}");

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string userId, string resourceName, CancellationToken cancellationToken)
        {
            this.Calls.Add($"delete:{resourceName}");

            return Task.CompletedTask;
        }

        private static string FormatClientData(IReadOnlyDictionary<string, string> clientData)
        {
            return string.Join(
                ",",
                clientData
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}={pair.Value}"));
        }
    }

    private sealed class FakePeopleContactClient : IGooglePeopleContactClient
    {
        public List<string> Calls { get; } = new();

        public Task<IReadOnlyList<GooglePersonContact>> ListAsync(string userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<GooglePersonContact>>(Array.Empty<GooglePersonContact>());
        }

        public Task CreateAsync(string userId, GooglePersonContact contact, CancellationToken cancellationToken)
        {
            this.Calls.Add($"create:{userId}:{contact.SourceId}:{contact.ResourceName}:{FormatGroups(contact)}");

            return Task.CompletedTask;
        }

        public Task UpdateAsync(string userId, GooglePersonContact contact, CancellationToken cancellationToken)
        {
            this.Calls.Add($"update:{userId}:{contact.SourceId}:{contact.ResourceName}:{FormatGroups(contact)}");

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string userId, string resourceName, CancellationToken cancellationToken)
        {
            this.Calls.Add($"delete:{userId}:{resourceName}");

            return Task.CompletedTask;
        }

        private static string FormatGroups(GooglePersonContact contact)
        {
            return string.Join(",", contact.ContactGroupResourceNames.Order(StringComparer.Ordinal));
        }
    }
}
