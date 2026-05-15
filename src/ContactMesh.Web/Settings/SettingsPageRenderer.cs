using System.Text;
using System.Text.Encodings.Web;
using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using ContactMesh.Google.Auth;
using ContactMesh.Microsoft365.Auth;

namespace ContactMesh.Web.Settings;

public static class SettingsPageRenderer
{
    public static string Render(
        ContactMeshOptions contactMesh,
        GoogleWorkspaceOptions googleWorkspace,
        Microsoft365Options microsoft365,
        string configPath,
        string? notice)
    {
        var html = new StringBuilder();

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine("<title>ContactMesh Settings</title>");
        html.AppendLine("<style>");
        html.AppendLine(Styles);
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<main class=\"page-shell\">");
        html.AppendLine("<aside class=\"sidebar\" aria-label=\"Settings navigation\">");
        html.AppendLine("<div class=\"brand-mark\" aria-hidden=\"true\">CM</div>");
        html.AppendLine("<nav>");
        html.AppendLine("<a href=\"#runtime\">Runtime</a>");
        html.AppendLine("<a href=\"#rules\">Rules</a>");
        html.AppendLine("<a href=\"#providers\">Providers</a>");
        html.AppendLine("</nav>");
        html.AppendLine("</aside>");
        html.AppendLine("<section class=\"workspace\">");
        html.AppendLine("<header class=\"toolbar\">");
        html.AppendLine("<div>");
        html.AppendLine("<p class=\"eyebrow\">ContactMesh</p>");
        html.AppendLine("<h1>Settings</h1>");
        html.AppendLine("</div>");
        html.AppendLine("<div class=\"status-strip\" aria-label=\"Run state\">");
        AppendStatusPill(html, contactMesh.Provider, "Provider");
        AppendStatusPill(html, contactMesh.DryRun ? "Dry run" : "Live writes", "Mode");
        html.AppendLine("</div>");
        html.AppendLine("</header>");
        if (!string.IsNullOrWhiteSpace(notice))
        {
            html.Append("<p class=\"notice\">");
            html.Append(Encode(notice));
            html.AppendLine("</p>");
        }

        html.AppendLine("<form method=\"post\" action=\"/settings\">");
        AppendRuntimeSection(html, contactMesh, configPath);
        AppendRulesSection(html, contactMesh.Rules, contactMesh.ManagedEmailDomains);
        AppendProvidersSection(html, googleWorkspace, microsoft365);
        html.AppendLine("<div class=\"save-bar\">");
        html.AppendLine("<button type=\"submit\">Save settings</button>");
        html.AppendLine("</div>");
        html.AppendLine("</form>");

        html.AppendLine("</section>");
        html.AppendLine("</main>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    private static void AppendRuntimeSection(StringBuilder html, ContactMeshOptions options, string configPath)
    {
        html.AppendLine("<section id=\"runtime\" class=\"band\">");
        html.AppendLine("<div class=\"band-header\">");
        html.AppendLine("<h2>Runtime</h2>");
        html.AppendLine("<span class=\"muted\">Read only</span>");
        html.AppendLine("</div>");
        html.AppendLine("<div class=\"settings-grid\">");
        AppendField(html, "Provider", "ContactMesh.Provider", options.Provider, "Selects which provider host ContactMesh uses for directory, group, and contact reads or writes.");
        AppendReadonlyField(html, "Config file", Path.GetFullPath(configPath), "The JSON file loaded first; environment variables and command-line values can override it.");
        AppendReadonlyField(html, "Config status", File.Exists(configPath) ? "Loaded from disk" : "Will be created on save", "If this does not point at appsettings.local.json, launch the Web app with that JSON path.");
        html.AppendLine("<label class=\"setting-row switch-row\">");
        html.AppendLine("<span>Dry run</span>");
        html.AppendLine("<small>When enabled, ContactMesh logs planned changes but skips provider writes. Keep this on for live-provider validation.</small>");
        html.Append("<input type=\"checkbox\" name=\"ContactMesh.DryRun\" value=\"true\"");
        if (options.DryRun)
        {
            html.Append(" checked");
        }

        html.AppendLine(">");
        html.AppendLine("</label>");
        html.AppendLine("</div>");
        html.AppendLine("</section>");
    }

    private static void AppendRulesSection(
        StringBuilder html,
        SyncRuleOptions rules,
        IReadOnlyList<string> managedEmailDomains)
    {
        html.AppendLine("<section id=\"rules\" class=\"band\">");
        html.AppendLine("<div class=\"band-header\">");
        html.AppendLine("<h2>Rules</h2>");
        html.AppendLine("<span class=\"muted\">ContactMesh:Rules</span>");
        html.AppendLine("</div>");

        html.AppendLine("<div class=\"rules-layout\">");
        AppendListField(html, "Managed domains", "ContactMesh.ManagedEmailDomains", "Email domains ContactMesh treats as organization-owned when cleaning duplicates, pruning stale contacts, and preferring work addresses.", managedEmailDomains);
        AppendField(html, "Main contacts group", "ContactMesh.Rules.MainContactsGroupEmail", rules.MainContactsGroupEmail, "Optional source group whose user members, including nested group members when the provider supplies them, become directory contacts instead of every eligible tenant user.");
        AppendField(html, "Main contacts label", "ContactMesh.Rules.MainContactsGroupLabel", ResolveDirectoryLabel(rules), "Label applied to directory contacts from the main contacts group. The legacy MainContactsGroupLable spelling is also accepted in config.");
        AppendListField(html, "Target users", "ContactMesh.Rules.TargetUsers", "Optional user IDs or email addresses that limit who receives managed contacts; source directory users remain eligible for those targets.", rules.TargetUsers);
        AppendListField(html, "Global user groups", "ContactMesh.Rules.GlobalUserGroups", "Groups whose user members should receive global managed contacts.", rules.GlobalUserGroups);
        AppendListField(html, "Global external contacts", "ContactMesh.Rules.GlobalExternalContactGroups", "Shared external contact groups that are copied into eligible targets.", rules.GlobalExternalContactGroups);
        AppendListField(html, "Exclusion groups", "ContactMesh.Rules.ExclusionGroups", "Users or group members that should not receive managed contacts.", rules.ExclusionGroups);
        AppendListField(html, "Scoped group roots", "ContactMesh.Rules.ScopedGroupRoots", "Root groups used for group-aware visibility, so targets receive contacts from groups they are allowed to see.", rules.ScopedGroupRoots);
        AppendListField(html, "Included OUs", "ContactMesh.Rules.IncludedOrganizationUnits", "Organization unit prefixes allowed to receive managed contacts.", rules.IncludedOrganizationUnits);
        AppendListField(html, "Excluded OUs", "ContactMesh.Rules.ExcludedOrganizationUnits", "Organization unit prefixes blocked from receiving managed contacts; append =Ignore to reduce expected noise.", rules.ExcludedOrganizationUnits);
        AppendMappings(html, rules.GroupMappings);
        html.AppendLine("</div>");
        html.AppendLine("</section>");
    }

    private static void AppendProvidersSection(
        StringBuilder html,
        GoogleWorkspaceOptions googleWorkspace,
        Microsoft365Options microsoft365)
    {
        html.AppendLine("<section id=\"providers\" class=\"band\">");
        html.AppendLine("<div class=\"band-header\">");
        html.AppendLine("<h2>Providers</h2>");
        html.AppendLine("<span class=\"muted\">Credentials hidden</span>");
        html.AppendLine("</div>");
        html.AppendLine("<div class=\"provider-grid\">");

        html.AppendLine("<section class=\"provider-panel\" aria-labelledby=\"google-heading\">");
        html.AppendLine("<h3 id=\"google-heading\">Google Workspace</h3>");
        AppendDetailInput(html, "Service account", "GoogleWorkspace.ServiceAccountFile", "Path to the delegated service-account credential file. Keep the file outside the repository.", googleWorkspace.ServiceAccountFile);
        AppendDetailInput(html, "Admin user", "GoogleWorkspace.AdminUserEmail", "Workspace admin account used for domain-wide delegated People API access.", googleWorkspace.AdminUserEmail);
        AppendDetailTextarea(html, "Scopes", "GoogleWorkspace.Scopes", "Google OAuth scopes requested by the delegated token provider.", googleWorkspace.Scopes);
        html.AppendLine("</section>");

        html.AppendLine("<section class=\"provider-panel\" aria-labelledby=\"microsoft-heading\">");
        html.AppendLine("<h3 id=\"microsoft-heading\">Microsoft 365</h3>");
        AppendDetailInput(html, "Tenant ID", "Microsoft365.TenantId", "Microsoft Entra tenant used for Graph client-credentials authentication.", microsoft365.TenantId);
        AppendDetailInput(html, "Client ID", "Microsoft365.ClientId", "Application registration ID granted Graph permissions for users, groups, memberships, and contacts.", microsoft365.ClientId);
        AppendDetailInput(html, "Client secret", "Microsoft365.ClientSecret", "Secret value is masked here; leave blank to keep the current secret.", null, string.IsNullOrWhiteSpace(microsoft365.ClientSecret) ? "Not configured" : "Configured");
        AppendDetailTextarea(html, "Scopes", "Microsoft365.Scopes", "Graph OAuth scopes requested by the client-credentials token provider.", microsoft365.Scopes);
        html.AppendLine("</section>");

        html.AppendLine("</div>");
        html.AppendLine("</section>");
    }

    private static void AppendStatusPill(StringBuilder html, string? value, string label)
    {
        html.Append("<span class=\"status-pill\"><span>");
        html.Append(Encode(label));
        html.Append("</span>");
        html.Append(Encode(Display(value)));
        html.AppendLine("</span>");
    }

    private static void AppendField(StringBuilder html, string label, string name, string? value, string description)
    {
        html.AppendLine("<label class=\"setting-row\">");
        html.Append("<span>");
        html.Append(Encode(label));
        html.AppendLine("</span>");
        html.Append("<small>");
        html.Append(Encode(description));
        html.AppendLine("</small>");
        html.Append("<input name=\"");
        html.Append(Encode(name));
        html.Append("\" value=\"");
        html.Append(string.IsNullOrWhiteSpace(value) ? string.Empty : Encode(value));
        html.AppendLine("\">");
        html.AppendLine("</label>");
    }

    private static void AppendReadonlyField(StringBuilder html, string label, string? value, string description)
    {
        html.AppendLine("<label class=\"setting-row\">");
        html.Append("<span>");
        html.Append(Encode(label));
        html.AppendLine("</span>");
        html.Append("<small>");
        html.Append(Encode(description));
        html.AppendLine("</small>");
        html.Append("<input value=\"");
        html.Append(Encode(Display(value)));
        html.AppendLine("\" readonly>");
        html.AppendLine("</label>");
    }

    private static void AppendListField(
        StringBuilder html,
        string title,
        string name,
        string description,
        IReadOnlyList<string> values)
    {
        html.AppendLine("<section class=\"rule-group\">");
        html.Append("<h3>");
        html.Append(Encode(title));
        html.AppendLine("</h3>");
        html.Append("<p class=\"description\">");
        html.Append(Encode(description));
        html.AppendLine("</p>");
        AppendTextarea(html, name, values);
        html.AppendLine("</section>");
    }

    private static void AppendMappings(StringBuilder html, IReadOnlyList<GroupMapping> mappings)
    {
        html.AppendLine("<section class=\"rule-group mappings\">");
        html.AppendLine("<h3>Group mappings</h3>");
        html.AppendLine("<p class=\"description\">Merge contacts from one source group into another target group label.</p>");
        html.Append("<textarea name=\"ContactMesh.Rules.GroupMappings\" rows=\"");
        html.Append(Math.Max(3, mappings.Count));
        html.AppendLine("\">");
        foreach (var mapping in mappings)
        {
            html.Append(Encode(mapping.From));
            html.Append(" -> ");
            html.AppendLine(Encode(mapping.To));
        }

        html.AppendLine("</textarea>");
        html.AppendLine("</section>");
    }

    private static void AppendDetailInput(
        StringBuilder html,
        string label,
        string name,
        string description,
        string? value,
        string? placeholder = null)
    {
        html.AppendLine("<div class=\"detail-row\">");
        html.AppendLine("<div>");
        html.Append("<span>");
        html.Append(Encode(label));
        html.AppendLine("</span>");
        html.Append("<small>");
        html.Append(Encode(description));
        html.AppendLine("</small>");
        html.AppendLine("</div>");
        html.Append("<input name=\"");
        html.Append(Encode(name));
        html.Append("\" value=\"");
        html.Append(string.IsNullOrWhiteSpace(value) ? string.Empty : Encode(value));
        html.Append("\"");
        if (!string.IsNullOrWhiteSpace(placeholder))
        {
            html.Append(" placeholder=\"");
            html.Append(Encode(placeholder));
            html.Append("\"");
        }

        html.AppendLine(">");
        html.AppendLine("</div>");
    }

    private static void AppendDetailTextarea(
        StringBuilder html,
        string label,
        string name,
        string description,
        IReadOnlyList<string> values)
    {
        html.AppendLine("<div class=\"detail-row detail-textarea\">");
        html.AppendLine("<div>");
        html.Append("<span>");
        html.Append(Encode(label));
        html.AppendLine("</span>");
        html.Append("<small>");
        html.Append(Encode(description));
        html.AppendLine("</small>");
        html.AppendLine("</div>");
        AppendTextarea(html, name, values);
        html.AppendLine("</div>");
    }

    private static void AppendTextarea(StringBuilder html, string name, IReadOnlyList<string> values)
    {
        html.Append("<textarea name=\"");
        html.Append(Encode(name));
        html.Append("\" rows=\"");
        html.Append(Math.Max(3, values.Count));
        html.AppendLine("\">");
        foreach (var value in values)
        {
            html.AppendLine(Encode(value));
        }

        html.AppendLine("</textarea>");
    }

    private static string Display(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not set" : value;
    }

    private static string ResolveDirectoryLabel(SyncRuleOptions rules)
    {
        if (!string.IsNullOrWhiteSpace(rules.MainContactsGroupLabel))
        {
            return rules.MainContactsGroupLabel;
        }

        return string.IsNullOrWhiteSpace(rules.MainContactsGroupLable)
            ? ContactSyncOrchestrator.DirectoryLabel
            : rules.MainContactsGroupLable;
    }

    private static string Encode(string value)
    {
        return HtmlEncoder.Default.Encode(value);
    }

    private const string Styles = """
:root {
  color-scheme: light;
  --ink: #20242a;
  --muted: #667085;
  --line: #d8dee7;
  --surface: #f6f7f9;
  --panel: #ffffff;
  --teal: #0f766e;
  --teal-soft: #d8f3ef;
  --amber: #b7791f;
  --amber-soft: #fff3d6;
  --indigo: #3f4c8f;
}

* {
  box-sizing: border-box;
}

body {
  margin: 0;
  min-height: 100vh;
  color: var(--ink);
  background: var(--surface);
  font-family: "Segoe UI", system-ui, -apple-system, BlinkMacSystemFont, sans-serif;
}

.page-shell {
  display: grid;
  grid-template-columns: 220px minmax(0, 1fr);
  min-height: 100vh;
}

.sidebar {
  background: #18212c;
  color: #f9fafb;
  padding: 24px 18px;
}

.brand-mark {
  display: grid;
  place-items: center;
  width: 48px;
  height: 48px;
  margin-bottom: 28px;
  border: 1px solid #5eead4;
  color: #ccfbf1;
  font-weight: 700;
}

nav {
  display: grid;
  gap: 8px;
}

nav a {
  color: #dbeafe;
  padding: 10px 12px;
  text-decoration: none;
  border-left: 3px solid transparent;
}

nav a:hover,
nav a:focus-visible {
  border-left-color: #fbbf24;
  background: #243142;
  outline: none;
}

.workspace {
  width: min(1160px, 100%);
  padding: 28px clamp(18px, 4vw, 44px) 48px;
}

.toolbar {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: 18px;
  margin-bottom: 26px;
}

.eyebrow {
  margin: 0 0 4px;
  color: var(--teal);
  font-size: 0.82rem;
  font-weight: 700;
  text-transform: uppercase;
}

h1,
h2,
h3,
p {
  margin: 0;
}

h1 {
  font-size: clamp(2rem, 4vw, 3rem);
  line-height: 1.04;
  letter-spacing: 0;
}

h2 {
  font-size: 1.18rem;
  letter-spacing: 0;
}

h3 {
  font-size: 0.95rem;
  letter-spacing: 0;
}

.status-strip {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 8px;
}

.status-pill,
.chip {
  display: inline-flex;
  align-items: center;
  min-height: 32px;
  border: 1px solid var(--line);
  background: var(--panel);
  padding: 6px 10px;
  font-size: 0.88rem;
  white-space: nowrap;
}

.status-pill span {
  margin-right: 8px;
  color: var(--muted);
  font-size: 0.75rem;
  text-transform: uppercase;
}

.band {
  padding: 24px 0;
  border-top: 1px solid var(--line);
}

.notice {
  margin-bottom: 18px;
  border: 1px solid #9adfd5;
  background: var(--teal-soft);
  color: #0f4f49;
  padding: 12px 14px;
}

.band-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 18px;
  margin-bottom: 16px;
}

.muted,
.empty {
  color: var(--muted);
}

.settings-grid,
.provider-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 14px;
}

.setting-row,
.provider-panel,
.rule-group {
  border: 1px solid var(--line);
  background: var(--panel);
}

.setting-row {
  display: grid;
  gap: 8px;
  padding: 14px;
  min-height: 86px;
}

.setting-row span,
.detail-row span {
  color: var(--muted);
  font-size: 0.82rem;
}

.setting-row small,
.detail-row small,
.description {
  display: block;
  color: var(--muted);
  font-size: 0.82rem;
  line-height: 1.35;
}

input,
textarea {
  width: 100%;
  min-width: 0;
  border: 1px solid var(--line);
  color: var(--ink);
  background: #ffffff;
  font: inherit;
  padding: 9px 10px;
}

textarea {
  min-height: 104px;
  resize: vertical;
  line-height: 1.35;
}

input[readonly] {
  color: var(--muted);
  background: #f9fafb;
}

.switch-row {
  align-content: start;
}

.switch-row input {
  width: 42px;
  height: 24px;
  accent-color: var(--teal);
}

.rules-layout {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 14px;
}

.rule-group {
  padding: 14px;
  min-height: 96px;
}

.rule-group h3,
.provider-panel h3 {
  margin-bottom: 8px;
}

.rule-group .description {
  margin-bottom: 12px;
}

.chips {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.chip {
  background: var(--teal-soft);
  border-color: #9adfd5;
  color: #0f4f49;
}

.mappings {
  grid-column: 1 / -1;
}

table {
  width: 100%;
  border-collapse: collapse;
}

th,
td {
  padding: 10px 8px;
  border-top: 1px solid var(--line);
  text-align: left;
}

th {
  color: var(--muted);
  font-size: 0.8rem;
  font-weight: 600;
}

.provider-panel {
  padding: 16px;
}

.detail-row {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  padding: 10px 0;
  border-top: 1px solid var(--line);
}

.detail-row input,
.detail-row textarea {
  min-width: 0;
  max-width: 64%;
}

.detail-textarea {
  align-items: start;
}

.save-bar {
  position: sticky;
  bottom: 0;
  display: flex;
  justify-content: flex-end;
  padding: 16px 0 0;
  border-top: 1px solid var(--line);
  background: var(--surface);
}

button {
  border: 1px solid #0f4f49;
  background: var(--teal);
  color: #ffffff;
  padding: 10px 16px;
  font: inherit;
  font-weight: 700;
}

@media (max-width: 820px) {
  .page-shell {
    grid-template-columns: 1fr;
  }

  .sidebar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 14px 18px;
  }

  .brand-mark {
    margin-bottom: 0;
  }

  nav {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
  }

  .toolbar,
  .band-header {
    align-items: start;
    flex-direction: column;
  }

  .status-strip {
    justify-content: flex-start;
  }

  .settings-grid,
  .provider-grid,
  .rules-layout {
    grid-template-columns: 1fr;
  }

  .detail-row {
    display: grid;
  }

  .detail-row input,
  .detail-row textarea {
    max-width: none;
  }
}
""";
}
