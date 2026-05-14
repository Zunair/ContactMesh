using System.Text;
using System.Text.Encodings.Web;
using ContactMesh.Core.Models;
using ContactMesh.Google.Auth;
using ContactMesh.Microsoft365.Auth;

namespace ContactMesh.Web.Settings;

public static class SettingsPageRenderer
{
    public static string Render(
        ContactMeshOptions contactMesh,
        GoogleWorkspaceOptions googleWorkspace,
        Microsoft365Options microsoft365,
        string configPath)
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

        AppendRuntimeSection(html, contactMesh, configPath);
        AppendRulesSection(html, contactMesh.Rules, contactMesh.ManagedEmailDomains);
        AppendProvidersSection(html, googleWorkspace, microsoft365);

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
        AppendField(html, "Provider", options.Provider);
        AppendField(html, "Config file", configPath);
        html.AppendLine("<label class=\"setting-row switch-row\">");
        html.AppendLine("<span>Dry run</span>");
        html.Append("<input type=\"checkbox\" disabled");
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
        AppendChipGroup(html, "Managed domains", managedEmailDomains);
        AppendChipGroup(html, "Global user groups", rules.GlobalUserGroups);
        AppendChipGroup(html, "Global external contacts", rules.GlobalExternalContactGroups);
        AppendChipGroup(html, "Exclusion groups", rules.ExclusionGroups);
        AppendChipGroup(html, "Scoped group roots", rules.ScopedGroupRoots);
        AppendChipGroup(html, "Included OUs", rules.IncludedOrganizationUnits);
        AppendChipGroup(html, "Excluded OUs", rules.ExcludedOrganizationUnits);
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
        AppendDetail(html, "Service account", googleWorkspace.ServiceAccountFile);
        AppendDetail(html, "Admin user", googleWorkspace.AdminUserEmail);
        AppendDetail(html, "Scopes", FormatCount(googleWorkspace.Scopes.Count));
        html.AppendLine("</section>");

        html.AppendLine("<section class=\"provider-panel\" aria-labelledby=\"microsoft-heading\">");
        html.AppendLine("<h3 id=\"microsoft-heading\">Microsoft 365</h3>");
        AppendDetail(html, "Tenant ID", microsoft365.TenantId);
        AppendDetail(html, "Client ID", microsoft365.ClientId);
        AppendDetail(html, "Client secret", string.IsNullOrWhiteSpace(microsoft365.ClientSecret) ? "Not configured" : "Configured");
        AppendDetail(html, "Scopes", FormatCount(microsoft365.Scopes.Count));
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

    private static void AppendField(StringBuilder html, string label, string? value)
    {
        html.AppendLine("<label class=\"setting-row\">");
        html.Append("<span>");
        html.Append(Encode(label));
        html.AppendLine("</span>");
        html.Append("<input value=\"");
        html.Append(Encode(Display(value)));
        html.AppendLine("\" readonly>");
        html.AppendLine("</label>");
    }

    private static void AppendChipGroup(StringBuilder html, string title, IReadOnlyList<string> values)
    {
        html.AppendLine("<section class=\"rule-group\">");
        html.Append("<h3>");
        html.Append(Encode(title));
        html.AppendLine("</h3>");

        if (values.Count == 0)
        {
            html.AppendLine("<p class=\"empty\">None</p>");
        }
        else
        {
            html.AppendLine("<div class=\"chips\">");
            foreach (var value in values)
            {
                html.Append("<span class=\"chip\">");
                html.Append(Encode(value));
                html.AppendLine("</span>");
            }

            html.AppendLine("</div>");
        }

        html.AppendLine("</section>");
    }

    private static void AppendMappings(StringBuilder html, IReadOnlyList<GroupMapping> mappings)
    {
        html.AppendLine("<section class=\"rule-group mappings\">");
        html.AppendLine("<h3>Group mappings</h3>");
        if (mappings.Count == 0)
        {
            html.AppendLine("<p class=\"empty\">None</p>");
        }
        else
        {
            html.AppendLine("<table>");
            html.AppendLine("<thead><tr><th>From</th><th>To</th></tr></thead>");
            html.AppendLine("<tbody>");
            foreach (var mapping in mappings)
            {
                html.Append("<tr><td>");
                html.Append(Encode(mapping.From));
                html.Append("</td><td>");
                html.Append(Encode(mapping.To));
                html.AppendLine("</td></tr>");
            }

            html.AppendLine("</tbody>");
            html.AppendLine("</table>");
        }

        html.AppendLine("</section>");
    }

    private static void AppendDetail(StringBuilder html, string label, string? value)
    {
        html.AppendLine("<div class=\"detail-row\">");
        html.Append("<span>");
        html.Append(Encode(label));
        html.Append("</span><strong>");
        html.Append(Encode(Display(value)));
        html.AppendLine("</strong>");
        html.AppendLine("</div>");
    }

    private static string Display(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not set" : value;
    }

    private static string FormatCount(int count)
    {
        return count == 1 ? "1 item" : $"{count} items";
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

input {
  width: 100%;
  min-width: 0;
  border: 0;
  color: var(--ink);
  background: transparent;
  font: inherit;
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

.detail-row strong {
  min-width: 0;
  max-width: 64%;
  overflow-wrap: anywhere;
  text-align: right;
  font-size: 0.92rem;
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

  .detail-row strong {
    max-width: none;
    text-align: left;
  }
}
""";
}
