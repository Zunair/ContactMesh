// File: GroupVisibilityRule.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;

namespace ContactMesh.Core.Rules
{
    public sealed class GroupVisibilityRule
    {
        public bool IsVisibleToTarget(MeshContact contact, SyncTarget target)
        {
            return contact.Labels.Count == 0 || contact.Labels.Overlaps(target.LabelNames);
        }

        public bool IsGroupVisibleToTarget(MeshGroup group, SyncTarget target)
        {
            return group.GroupVisibility switch
            {
                MeshGroupVisibility.Domain => true,
                MeshGroupVisibility.Members => HasMember(group, target),
                _ => false
            };
        }

        public bool CanTargetSeeGroupMembers(MeshGroup group, SyncTarget target)
        {
            return group.MemberVisibility switch
            {
                MeshGroupVisibility.Domain => true,
                MeshGroupVisibility.Members => HasMember(group, target),
                _ => false
            };
        }

        public IReadOnlyList<GroupVisibilityDecision> FilterVisibleGroups(IEnumerable<MeshGroup> groups, SyncTarget target)
        {
            return groups
                .Where(group => IsGroupVisibleToTarget(group, target))
                .Select(group => new GroupVisibilityDecision(group, CanTargetSeeGroupMembers(group, target)))
                .ToList();
        }

        private static bool HasMember(MeshGroup group, SyncTarget target)
        {
            var targetEmails = GetTargetEmails(target).ToHashSet(StringComparer.OrdinalIgnoreCase);

            return group.Members.Any(member =>
                string.Equals(member.Id, target.UserId, StringComparison.OrdinalIgnoreCase)
                || GetMemberEmails(member).Any(targetEmails.Contains));
        }

        private static IEnumerable<string> GetTargetEmails(SyncTarget target)
        {
            if (!string.IsNullOrWhiteSpace(target.UserEmail))
            {
                yield return target.UserEmail;
            }

            foreach (var email in target.AlternateEmails.Where(email => !string.IsNullOrWhiteSpace(email)))
            {
                yield return email;
            }
        }

        private static IEnumerable<string> GetMemberEmails(MeshGroupMember member)
        {
            if (!string.IsNullOrWhiteSpace(member.Email))
            {
                yield return member.Email;
            }

            foreach (var email in member.AlternateEmails.Where(email => !string.IsNullOrWhiteSpace(email)))
            {
                yield return email;
            }
        }
    }

    public sealed record GroupVisibilityDecision(MeshGroup Group, bool CanSeeMembers);
}
