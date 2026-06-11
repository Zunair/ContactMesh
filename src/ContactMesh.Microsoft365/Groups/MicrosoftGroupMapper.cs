// File: MicrosoftGroupMapper.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Groups
{
    public static class MicrosoftGroupMapper
    {
        public const string OrgContactIdMetadataKey = "microsoft.graph.orgContactId";

        public static MeshGroup ToMeshGroup(
            MicrosoftGraphGroup group,
            IEnumerable<MicrosoftGraphGroupMember> members)
        {
            ArgumentNullException.ThrowIfNull(group);
            ArgumentNullException.ThrowIfNull(members);

            return new MeshGroup
            {
                Id = RequireValue(group.Id, "Graph group id"),
                Email = RequireValue(group.Mail, "Graph group mail"),
                DisplayName = group.DisplayName,
                GroupVisibility = ToGroupVisibility(group.Visibility),
                MemberVisibility = ToMemberVisibility(group.Visibility),
                Members = members
                    .Select(ToMeshGroupMember)
                    .OfType<MeshGroupMember>()
                    .ToList()
            };
        }

        public static MeshGroupMember? ToMeshGroupMember(MicrosoftGraphGroupMember member)
        {
            ArgumentNullException.ThrowIfNull(member);

            var id = member.Id?.Trim();
            var identity = ResolveEmailIdentity(member.Mail, member.UserPrincipalName, member.ProxyAddresses);
            var email = identity.PrimaryEmail;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            return new MeshGroupMember
            {
                Id = id,
                Email = email,
                DisplayName = member.DisplayName,
                Type = ToMemberType(member.ODataType),
                AlternateEmails = identity.AlternateEmails,
                Warnings = BuildWarnings("Microsoft 365 group member", id, member.DisplayName, identity)
            };
        }

        public static MeshContact ToMeshContact(MicrosoftGraphGroupMember member)
        {
            ArgumentNullException.ThrowIfNull(member);

            var id = RequireValue(member.Id, "Graph organizational contact id");
            var email = RequireValue(member.Mail, "Graph organizational contact mail");

            return new MeshContact
            {
                SourceId = $"orgContact:{id}",
                DisplayName = member.DisplayName,
                GivenName = member.GivenName,
                FamilyName = member.Surname,
                CompanyName = member.CompanyName,
                Department = member.Department,
                JobTitle = member.JobTitle,
                Emails = new[] { new ContactEmail(email, "work", true) },
                MatchEmails = GetDistinctEmails(new[] { member.Mail, member.UserPrincipalName }
                    .Concat(GetSmtpProxyAddresses(member.ProxyAddresses))),
                Phones = ToPhones(member).ToList(),
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [OrgContactIdMetadataKey] = id
                }
            };
        }

        /// <summary>
        /// Classifies a Graph group as Microsoft365, MailEnabledSecurity, or Distribution.
        /// Returns null for non-mail-enabled groups (security-only groups without an email address).
        /// </summary>
        public static MicrosoftGroupType? GetGroupType(MicrosoftGraphGroup group)
        {
            ArgumentNullException.ThrowIfNull(group);

            if (group.GroupTypes.Any(t => string.Equals(t, "Unified", StringComparison.OrdinalIgnoreCase)))
            {
                return MicrosoftGroupType.Microsoft365;
            }

            if (group.MailEnabled == true && group.SecurityEnabled == true)
            {
                return MicrosoftGroupType.MailEnabledSecurity;
            }

            if (group.MailEnabled == true)
            {
                return MicrosoftGroupType.Distribution;
            }

            return null;
        }

        public static bool IsOrgContact(MicrosoftGraphGroupMember member)
        {
            ArgumentNullException.ThrowIfNull(member);

            return string.Equals(
                NormalizeODataType(member.ODataType),
                "microsoft.graph.orgContact",
                StringComparison.OrdinalIgnoreCase);
        }

        public static MeshGroupVisibility ToGroupVisibility(string? visibility)
        {
            return visibility?.Trim().ToUpperInvariant() switch
            {
                "PUBLIC" => MeshGroupVisibility.Domain,
                "PRIVATE" => MeshGroupVisibility.Members,
                "HIDDENMEMBERSHIP" => MeshGroupVisibility.Members,
                _ => MeshGroupVisibility.Hidden
            };
        }

        public static MeshGroupVisibility ToMemberVisibility(string? visibility)
        {
            return visibility?.Trim().ToUpperInvariant() switch
            {
                "PUBLIC" => MeshGroupVisibility.Domain,
                "PRIVATE" => MeshGroupVisibility.Members,
                "HIDDENMEMBERSHIP" => MeshGroupVisibility.Hidden,
                _ => MeshGroupVisibility.Hidden
            };
        }

        private static MeshGroupMemberType ToMemberType(string? odataType)
        {
            return NormalizeODataType(odataType) switch
            {
                "microsoft.graph.user" => MeshGroupMemberType.User,
                "microsoft.graph.group" => MeshGroupMemberType.Group,
                "microsoft.graph.orgContact" => MeshGroupMemberType.Contact,
                _ => MeshGroupMemberType.Unknown
            };
        }

        private static IEnumerable<ContactPhone> ToPhones(MicrosoftGraphGroupMember member)
        {
            foreach (var phone in member.BusinessPhones.Where(phone => !string.IsNullOrWhiteSpace(phone)))
            {
                yield return new ContactPhone(phone.Trim(), "work");
            }

            if (!string.IsNullOrWhiteSpace(member.MobilePhone))
            {
                yield return new ContactPhone(member.MobilePhone.Trim(), "mobile");
            }
        }

        private static string? NormalizeODataType(string? odataType)
        {
            var value = odataType?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.StartsWith('#') ? value[1..] : value;
        }

        private static string RequireValue(string? value, string name)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw new InvalidOperationException($"{name} must be present.");
            }

            return trimmed;
        }

        private static string? FirstValueOrNull(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
        }

        private static EmailIdentity ResolveEmailIdentity(
            string? mail,
            string? userPrincipalName,
            IReadOnlyList<string> proxyAddresses)
        {
            var primaryProxy = GetPrimaryProxyAddress(proxyAddresses);
            var primaryEmail = FirstValueOrNull(primaryProxy, mail, userPrincipalName);
            var alternates = GetDistinctEmails(new[] { mail, userPrincipalName }
                    .Concat(GetSmtpProxyAddresses(proxyAddresses)))
                .Where(email => !string.Equals(email, primaryEmail, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return new EmailIdentity(
                primaryEmail,
                mail?.Trim(),
                userPrincipalName?.Trim(),
                primaryProxy,
                alternates);
        }

        private static IReadOnlyList<string> BuildWarnings(
            string subject,
            string id,
            string? displayName,
            EmailIdentity identity)
        {
            var compared = GetDistinctEmails(new[]
            {
                identity.PrimaryEmail,
                identity.Mail,
                identity.UserPrincipalName,
                identity.PrimaryProxyAddress
            });
            if (compared.Count <= 1)
            {
                return Array.Empty<string>();
            }

            var name = string.IsNullOrWhiteSpace(displayName) ? id : $"{displayName.Trim()} ({id})";
            return new[]
            {
                $"{subject} {name} has mismatched email identity values: selectedPrimary={identity.PrimaryEmail ?? "(blank)"}; mail={identity.Mail ?? "(blank)"}; userPrincipalName={identity.UserPrincipalName ?? "(blank)"}; primaryProxy={identity.PrimaryProxyAddress ?? "(blank)"}. ContactMesh will match aliases, but account cleanup may be needed."
            };
        }

        private static string? GetPrimaryProxyAddress(IEnumerable<string> proxyAddresses)
        {
            return proxyAddresses
                .Select(address => address?.Trim())
                .FirstOrDefault(address => address is not null && address.StartsWith("SMTP:", StringComparison.Ordinal))
                ?[5..];
        }

        private static IEnumerable<string> GetSmtpProxyAddresses(IEnumerable<string> proxyAddresses)
        {
            foreach (var proxyAddress in proxyAddresses)
            {
                var value = proxyAddress?.Trim();
                if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("smtp:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var email = value[5..].Trim();
                if (!string.IsNullOrWhiteSpace(email))
                {
                    yield return email;
                }
            }
        }

        private static IReadOnlyList<string> GetDistinctEmails(IEnumerable<string?> emails)
        {
            return emails
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private sealed record EmailIdentity(
            string? PrimaryEmail,
            string? Mail,
            string? UserPrincipalName,
            string? PrimaryProxyAddress,
            IReadOnlyList<string> AlternateEmails);
    }
}
