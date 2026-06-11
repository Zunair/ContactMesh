// File: OrganizationUnitRule.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;

namespace ContactMesh.Core.Rules
{
    public sealed class OrganizationUnitRule
    {
        private readonly IReadOnlyList<string> includedPrefixes;
        private readonly IReadOnlyList<OrganizationUnitExclusion> excludedPrefixes;

        public OrganizationUnitRule(IEnumerable<string>? includedOrganizationUnits = null, IEnumerable<string>? excludedOrganizationUnits = null)
        {
            this.includedPrefixes = (includedOrganizationUnits ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(NormalizePath)
                .ToList();

            this.excludedPrefixes = (excludedOrganizationUnits ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(ParseExclusion)
                .ToList();
        }

        public OrganizationUnitEvaluation Evaluate(MeshUser user)
        {
            var organizationUnit = NormalizePath(user.OrganizationUnit);
            var exclusion = this.excludedPrefixes.FirstOrDefault(rule => IsMatch(organizationUnit, rule.Path));

            if (exclusion is not null)
            {
                return new OrganizationUnitEvaluation
                {
                    IsIncluded = false,
                    IsIgnored = exclusion.Ignore,
                    Reason = $"User organization unit '{organizationUnit}' is excluded by '{exclusion.Path}'."
                };
            }

            if (this.includedPrefixes.Count > 0 && !this.includedPrefixes.Any(prefix => IsMatch(organizationUnit, prefix)))
            {
                return new OrganizationUnitEvaluation
                {
                    IsIncluded = false,
                    Reason = $"User organization unit '{organizationUnit}' is not in the included organization units."
                };
            }

            return new OrganizationUnitEvaluation
            {
                IsIncluded = true,
                Reason = $"User organization unit '{organizationUnit}' is included."
            };
        }

        private static OrganizationUnitExclusion ParseExclusion(string value)
        {
            var parts = value.Split('=', 2, StringSplitOptions.TrimEntries);

            return new OrganizationUnitExclusion(
                NormalizePath(parts[0]),
                parts.Length > 1 && string.Equals(parts[1], "Ignore", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsMatch(string organizationUnit, string prefix)
        {
            return organizationUnit.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "/";
            }

            var trimmed = value.Trim();
            return trimmed.StartsWith('/') ? trimmed : $"/{trimmed}";
        }
    }

    public sealed record OrganizationUnitEvaluation
    {
        public required bool IsIncluded { get; init; }
        public bool IsIgnored { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    internal sealed record OrganizationUnitExclusion(string Path, bool Ignore);
}
