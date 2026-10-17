using System.Net;
using System.Text.Json;
using ArchipelagoSphereTracker.src.Resources;

public static class WebPortalCommandsPage
{
    public static string Build()
    {
        static string T(string key) => Resource.ResourceManager.GetString(key) ?? key;
        static string Js(string key) => JsonSerializer.Serialize(T(key));
        var templatesPath = Path.Combine(Declare.BasePath, "extern", "Archipelago", "Players", "Templates");
        var templateOptions = Directory.Exists(templatesPath)
            ? Directory.EnumerateFiles(templatesPath, "*.yaml")
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name => $"<option value=\"{WebUtility.HtmlEncode(name)}\">{WebUtility.HtmlEncode(name)}</option>")
                .ToList()
            : new List<string>();

        var templateSelectOptions = templateOptions.Any()
            ? string.Join(Environment.NewLine, templateOptions)
            : $@"<option value=\""disabled selected>{T("WebNoTemplateAvailable")}</option>";

        var modeLabel = Declare.IsArchipelagoMode ? "Archipelago" : "Standard";
        var archipelagoSections = Declare.IsArchipelagoMode
            ? $@"
    <section class=""panel"">
      <h2>{T("PrepTitle")}</h2>
      <form data-command=""organize-room"">
        <label>{T("PrepName")}<input name=""name"" maxlength=""80"" required /></label>
        <label>{T("PrepCapacity")}<input type=""number"" name=""maximumPlayers"" min=""1"" max=""200"" value=""20"" required /></label>
        <button type=""submit"">{T("PrepCreate")}</button><div class=""result"" data-result></div>
      </form>
    </section>
    <section class=""panel"">
      <h2>YAML</h2>
      <form data-command=""list-yamls"">
        <button type=""submit"">{T("WebListYamls")}</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""backup-yamls"">
        <button type=""submit"">Backup YAML (ZIP)</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""delete-yaml"">
        <label>{T("WebYamlFileToDelete")}
          <select name=""fileName"" data-yaml-select required>
            <option value="""" selected disabled>{T("WebLoadingYamls")}</option>
          </select>
        </label>
        <button type=""submit"">{T("WebDeleteYaml")}</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""clean-yamls"">
        <button type=""submit"">{T("WebCleanAllYamls")}</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""download-yaml"">
        <label>{T("WebYamlFileToDownload")}
          <select name=""fileName"" data-yaml-select required>
            <option value="""" selected disabled>{T("WebLoadingYamls")}</option>
          </select>
        </label>
        <button type=""submit"">{T("WebDownloadYaml")}</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""send-yaml"">
        <label>{T("WebUploaderUnYAML")}
          <input type=""file"" name=""file"" accept="".yaml"" required />
        </label>
        <button type=""submit"">{T("WebSendYaml")}</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""download-template"">
        <label>Template YAML
          <select name=""template"" required>
            {templateSelectOptions}
          </select>
        </label>
        <button type=""submit"">{T("WebDownloadTemplate")}</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>APWorld</h2>
      <form data-command=""list-apworld"">
        <button type=""submit"">{T("WebAPWorldList")}</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""backup-apworld"">
        <button type=""submit"">Backup APWorld (ZIP)</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""send-apworld"">
        <label>Uploader un APWorld
          <input type=""file"" name=""file"" accept="".apworld"" required />
        </label>
        <button type=""submit"">{T("WebSendApworld")}</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>{T("WebGeneration")}</h2>
      <form data-command=""generate"">
        <label>{T("WebProgressionBalancing")}
          <select name=""skipProgBalancing"">
            <option value=""false"">{T("AstCenterNormalBalancing")}</option>
            <option value=""true"">{T("AstCenterSkipBalancing")}</option>
          </select>
        </label>
        <button type=""submit"">{T("WebGenerate")}</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""test-generate"">
        <button type=""submit"">{T("WebTestGeneration")}</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""generate-with-zip"">
        <label>ZIP de YAML
          <input type=""file"" name=""file"" accept="".zip"" required />
        </label>
        <label>{T("WebProgressionBalancing")}
          <select name=""skipProgBalancing"">
            <option value=""false"">{T("AstCenterNormalBalancing")}</option>
            <option value=""true"">{T("AstCenterSkipBalancing")}</option>
          </select>
        </label>
        <button type=""submit"">{T("WebGenerateWithZip")}</button>
        <div class=""result"" data-result></div>
      </form>
    </section>"
            : string.Empty;

        return $@"<!doctype html>
<html lang=""{Declare.Language}"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>AST · {T("WebAdminPortalTitle")}</title>
  <style>{WebPortalTheme.Styles}</style>
</head>
<body>
  <header>
    <div class=""hero"">
      <div class=""title"">
        <h1>AST · {T("WebAdminPortalTitle")}</h1>
        <span>⚙️ {T("WebDiscordCommands")}</span>
      </div>
      <div class=""badge"">Mode: {WebUtility.HtmlEncode(modeLabel)}</div>
    </div>
    <div class=""meta"" id=""channel-meta"">Channel ID: —</div>
  </header>

  <main class=""management-layout"">
    <section class=""panel"">
      <h2>{T("TrackerRooms")}</h2>
      <p class=""mode"">{T("WebAvailableLinksForGuild")}</p>
      <label>{T("TrackerSearch")}<input id=""room-search"" type=""search"" placeholder=""{T("WebThreadName")}"" /></label>
      <div id=""room-links"" class=""table-wrap""><p class=""empty"">{T("WebLoadingAstRoomPortals")}</p></div>
    </section>

    <section class=""panel"">
      <h2>{T("WebCreateThreadViaAddUrl")}</h2>
      <form data-command=""add-url"">
        <label>URL Archipelago
          <input name=""url"" placeholder=""https://archipelago.gg/room/XXXX"" required />
        </label>
        <label>{T("WebThreadName")}
          <input name=""threadName"" placeholder=""Archipelago"" />
        </label>
        <label>{T("WebThreadType")}
          <select name=""threadType"">
            <option value=""Private"">{T("WebPrivate")}</option>
            <option value=""Public"">Public</option>
          </select>
        </label>
        <label>{T("WebAutoAddMembersPublic")}
          <select name=""autoAddMembers"">
            <option value=""false"">{T("WebNo")}</option>
            <option value=""true"">{T("WebYes")}</option>
          </select>
        </label>
        <label>{T("WebSilentMode")}
          <select name=""silent"">
            <option value=""false"">{T("WebNo")}</option>
            <option value=""true"">{T("WebYes")}</option>
          </select>
        </label>
        <label>{T("WebPollingMode")}
          <select name=""pollingMode"">
            <option value=""automatic"">{T("WebPollingAutomatic")}</option>
            <option value=""fixed"">{T("WebPollingFixedFrequency")}</option>
          </select>
        </label>
        <label data-fixed-frequency hidden>{T("WebPollingFixedFrequency")}
          <select name=""checkFrequency"" disabled>
            <option value=""5m"">{T("WebEvery5Minutes")}</option>
            <option value=""15m"">{T("WebEvery15Minutes")}</option>
            <option value=""30m"">{T("WebEvery30Minutes")}</option>
            <option value=""1h"">{T("WebEvery1h")}</option>
            <option value=""6h"">{T("WebEvery6h")}</option>
            <option value=""12h"">{T("WebEvery12h")}</option>
            <option value=""18h"">{T("WebEvery18h")}</option>
            <option value=""1d"">{T("WebEveryDay")}</option>
          </select>
        </label>
        <button type=""submit"">{T("WebCreateThread")}</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>{T("WebUsefulInfo")}</h2>
      <form data-command=""ast-health"">
        <button type=""submit"">{T("WebOverallAstHealth")}</button>
        <div class=""result"" data-result></div>
      </form>
      <form data-command=""apworlds-info"">
        <button type=""submit"">APWorlds info</button>
        <div class=""result"" data-result></div>
      </form>
      <form data-command=""discord"">
        <button type=""submit"">Discord</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>{T("WebAuditLog")}</h2>
      <button type=""button"" id=""refresh-audit-button"">{T("WebRefreshAudit")}</button>
      <ul id=""audit-entries"" class=""room-links-list""></ul>
    </section>

    <section class=""panel"">
      <h2>{T("AstCenterRevokePortal")}</h2>
      <button type=""button"" id=""revoke-portal-button"">{T("AstCenterRevokeLink")}</button>
      <div class=""result"" id=""revoke-portal-result""></div>
    </section>

    {archipelagoSections}
  </main>

<script>
  const addUrlForm = document.querySelector('form[data-command=""add-url""]');
  const pollingModeSelect = addUrlForm.querySelector('[name=""pollingMode""]');
  const fixedFrequencyField = addUrlForm.querySelector('[data-fixed-frequency]');
  const fixedFrequencySelect = addUrlForm.querySelector('[name=""checkFrequency""]');
  const updatePollingFields = () => {{
    const fixed = pollingModeSelect.value === 'fixed';
    fixedFrequencyField.hidden = !fixed;
    fixedFrequencySelect.disabled = !fixed;
    fixedFrequencySelect.required = fixed;
  }};
  pollingModeSelect.addEventListener('change', updatePollingFields);
  updatePollingFields();
  const params = new URLSearchParams(window.location.search);
  if (window.__astPortalCommandsInitialized) {{
    console.debug('AST portal commands page already initialized; skipping duplicate init.');
  }} else {{
    window.__astPortalCommandsInitialized = true;


  // Attend /portal/{{guildId}}/{{channelId}}/{{token}}/commands.html
  const m = window.location.pathname.match(/\/portal\/(\d+)\/(\d+)\/([a-f0-9]+)\/commands\.html$/i);

  const guildId = params.get('guildId') || (m ? m[1] : '');
  const channelId = params.get('channelId') || (m ? m[2] : '');
  const token = m ? m[3] : '';

  const meta = document.getElementById('channel-meta');
  meta.textContent = channelId ? ('Channel ID: ' + channelId) : 'Channel ID: —';

  // Supporte un hébergement sous préfixe (/AST/portal/...)
  const path = window.location.pathname;
  const idx = path.indexOf('/portal/');
  const basePath = idx >= 0 ? path.substring(0, idx) : '';

  // Base API (garde le préfixe dynamique)
  const apiBase =
    window.location.origin +
    basePath +
    '/api/portal/' +
    guildId +
    '/' +
    channelId +
    '/' +
    token +
    '/commands/execute';

  const yamlsApi =
    window.location.origin +
    basePath +
    '/api/portal/' +
    guildId +
    '/' +
    channelId +
    '/' +
    token +
    '/commands/yamls';

  const yamlSelects = document.querySelectorAll('[data-yaml-select]');
  const roomLinksRoot = document.getElementById('room-links');
  const auditEntriesRoot = document.getElementById('audit-entries');
  const refreshAuditButton = document.getElementById('refresh-audit-button');
  const revokePortalButton = document.getElementById('revoke-portal-button');
  const revokePortalResult = document.getElementById('revoke-portal-result');

  const roomLinksApi =
    window.location.origin +
    basePath +
    '/api/portal/' +
    guildId +
    '/' +
    channelId +
    '/' +
    token +
    '/room-links';

  const auditApi =
    window.location.origin + basePath + '/api/portal/' + guildId + '/' + channelId + '/' + token + '/audit';
  const revokeApi =
    window.location.origin + basePath + '/api/portal/' + guildId + '/' + channelId + '/' + token + '/revoke';

  // Corrige les URL de download renvoyées par l’API (ex: /portal/... -> /AST/portal/...)
  const normalizeDownloadUrl = (u) => {{
    if (!u) return null;

    // Si déjà absolu (http/https), ne rien faire
    if (/^https?:\/\//i.test(u)) return u;

    // Si l'API renvoie déjà /AST/portal/..., ne pas doubler
    if (basePath && u.startsWith(basePath + '/')) return u;

    // Si l’API renvoie un chemin absolu à la racine (/portal/...), on ajoute le basePath (/AST)
    if (u.startsWith('/')) return basePath + u;

    // Sinon : chemin relatif -> on le rend absolu sous basePath
    return basePath + '/' + u.replace(/^\/+/, '');
  }};

  const showResult = (container, message, downloadUrl) => {{
    container.innerHTML = '';

    if (message) {{
      const msg = document.createElement('div');
      msg.textContent = message;
      container.appendChild(msg);
    }}

    const fixedUrl = normalizeDownloadUrl(downloadUrl);
    if (fixedUrl) {{
      const link = document.createElement('a');
      link.href = fixedUrl;
      link.textContent = '{T("WebDownloadFile")}';
      link.target = '_blank';
      link.rel = 'noopener noreferrer';
      container.appendChild(link);
    }}
  }};

  const parsePayload = async (response) => {{
    const raw = await response.text();
    if (!raw) return null;
    try {{
      return JSON.parse(raw);
    }} catch {{
      return raw; // plain text
    }}
  }};

  const extractMessage = (payload, fallback) => {{
    if (payload == null) return fallback;
    if (typeof payload === 'string') return payload || fallback;
    if (typeof payload === 'object' && payload.message) return payload.message;
    return fallback;
  }};

  const setYamlSelectOptions = (optionsMarkup) => {{
    yamlSelects.forEach((select) => {{
      select.innerHTML = optionsMarkup;
    }});
  }};

  let portalRooms = [];
  const roomSearch = document.getElementById('room-search');
  const showRoomMessage = message => {{
    const empty = document.createElement('p'); empty.className = 'empty'; empty.textContent = message;
    roomLinksRoot.replaceChildren(empty);
  }};
  const renderRoomLinks = () => {{
    const query = roomSearch.value.trim().toLocaleLowerCase();
    const rooms = portalRooms.filter(room => (room.threadName || '').toLocaleLowerCase().includes(query));
    if (!rooms.length) {{ showRoomMessage({Js("WebNoAstRoomPortalForGuild")}); return; }}
    const table = document.createElement('table'); table.className = 'tracker-table rooms-table';
    const head = table.createTHead().insertRow();
    [{Js("WebThreadName")}, {Js("WebOpen")}].forEach(text => {{
      const th = document.createElement('th'); th.scope = 'col'; th.textContent = text; head.appendChild(th);
    }});
    const body = table.createTBody();
    rooms.slice().sort((a,b) => (a.threadName || '').localeCompare(b.threadName || '')).forEach(room => {{
      const row = body.insertRow(); row.insertCell().textContent = room.threadName || ('Thread ' + (room.channelId || '?'));
      const link = document.createElement('a'); link.href = normalizeDownloadUrl(room.url || '') || '#';
      link.textContent = {Js("WebOpen")}; link.target = '_blank'; link.rel = 'noopener noreferrer';
      row.insertCell().appendChild(link);
    }});
    roomLinksRoot.replaceChildren(table);
  }};
  roomSearch.addEventListener('input', renderRoomLinks);
  const loadRoomLinks = async () => {{
    if (!roomLinksRoot) return;
    if (!guildId) {{
      showRoomMessage({Js("WebGuildIdMissingInUrl")});
      return;
    }}

    showRoomMessage({Js("WebLoadingAstRoomPortals")});

    try {{
      const response = await fetch(roomLinksApi);
      const payload = await parsePayload(response);
      const links = payload && typeof payload === 'object' && Array.isArray(payload.links)
        ? payload.links
        : [];

      if (!response.ok || links.length === 0) {{
        showRoomMessage({Js("WebNoAstRoomPortalForGuild")});
        return;
      }}

      portalRooms = links;
      renderRoomLinks();
    }} catch {{
      showRoomMessage({Js("WebUnableToLoadAstRoomPortals")});
    }}
  }};

  const loadYamlOptions = async () => {{
    if (!yamlSelects.length || !guildId || !channelId) return;

    setYamlSelectOptions('<option value="" selected disabled>{T("WebLoadingYamls")}</option>');

    try {{
      const response = await fetch(yamlsApi);
      const payload = await parsePayload(response);
      const files = payload && typeof payload === 'object' && Array.isArray(payload.files)
        ? payload.files
        : [];

      if (!response.ok || files.length === 0) {{
        setYamlSelectOptions('<option value="" selected disabled>{T("WebNoYamlAvailable")}</option>');
        return;
      }}

      yamlSelects.forEach((select) => {{
        select.innerHTML = '<option value="" selected disabled>{T("WebSelectYaml")}</option>';
        files.forEach((fileName) => {{
          const option = document.createElement('option');
          option.value = fileName;
          option.textContent = fileName;
          select.appendChild(option);
        }});
      }});
    }} catch {{
      setYamlSelectOptions('<option value="" selected disabled>{T("WebErrorLoadingYamls")}</option>');
    }}
  }};

  const loadAuditEntries = async () => {{
    if (!auditEntriesRoot) return;
    auditEntriesRoot.innerHTML = '<li>' + {Js("WebProcessing")} + '</li>';
    try {{
      const response = await fetch(auditApi + '?limit=100');
      const payload = await parsePayload(response);
      const entries = response.ok && payload && Array.isArray(payload.entries) ? payload.entries : [];
      auditEntriesRoot.innerHTML = '';
      if (entries.length === 0) {{
        auditEntriesRoot.innerHTML = '<li>' + {Js("WebNoAuditEntries")} + '</li>';
        return;
      }}

      entries.forEach(entry => {{
        const item = document.createElement('li');
        const occurredAt = entry.occurredAtUtc || entry.OccurredAtUtc;
        const source = entry.source || entry.Source || '';
        const action = entry.action || entry.Action || '';
        const outcome = entry.outcome || entry.Outcome || '';
        const targetChannel = entry.channelId || entry.ChannelId || '—';
        item.textContent = new Date(occurredAt).toLocaleString() + ' · ' + source + ' · ' + action + ' · ' + outcome + ' · #' + targetChannel;
        auditEntriesRoot.appendChild(item);
      }});
    }} catch {{
      auditEntriesRoot.innerHTML = '<li>' + {Js("WebUnableToReachServer")} + '</li>';
    }}
  }};

  const revokePortal = async () => {{
    if (!window.confirm({Js("WebConfirmRevokePortal")})) return;
    revokePortalResult.textContent = {Js("WebProcessing")};
    try {{
      const response = await fetch(revokeApi, {{ method: 'POST' }});
      const payload = await parsePayload(response);
      revokePortalResult.textContent = extractMessage(
        payload,
        response.ok ? {Js("AstCenterThePortalLinkWasRevoked")} : {Js("WebCommandError")});
      if (response.ok) {{
        document.querySelectorAll('button, select, input').forEach(element => {{ element.disabled = true; }});
      }}
    }} catch {{
      revokePortalResult.textContent = {Js("WebUnableToReachServer")};
    }}
  }};

  refreshAuditButton.addEventListener('click', loadAuditEntries);
  revokePortalButton.addEventListener('click', revokePortal);


  document.querySelectorAll('form[data-command]').forEach((form) => {{
    form.addEventListener('submit', async (event) => {{
      event.preventDefault();

      const result = form.querySelector('[data-result]');
      if (!guildId || !channelId) {{
        showResult(
          result,
          {Js("WebInvalidUrlOpenViaPortal")},
          null
        );
        return;
      }}

      if (['delete-yaml', 'clean-yamls'].includes(form.dataset.command) &&
          !window.confirm({Js("WebConfirmDangerousAction")})) {{
        return;
      }}

      const data = new FormData(form);
      data.set('command', form.dataset.command);

      if (['delete-yaml', 'download-yaml'].includes(form.dataset.command)) {{
        const selectedYaml = data.get('fileName');
        if (!selectedYaml) {{
          showResult(result, '{T("WebNoYamlSelected")}', null);
          return;
        }}
      }}

      showResult(result, '{T("WebProcessing")}', null);

      try {{
        const response = await fetch(apiBase, {{
          method: 'POST',
          body: data
        }});

        const payload = await parsePayload(response);
        const msg = extractMessage(
          payload,
          response.ok ? '{T("WebCommandExecuted")}' : '{T("WebCommandError")}'
        );

        if (!response.ok) {{
          showResult(result, msg, null);
          return;
        }}

        const downloadUrl =
          payload && typeof payload === 'object' ? payload.downloadUrl : null;

        // IMPORTANT : showResult corrigera /portal/... en /AST/portal/... automatiquement
        showResult(result, msg, downloadUrl);

        if (['send-yaml', 'delete-yaml', 'clean-yamls'].includes(form.dataset.command)) {{
          await loadYamlOptions();
        }}
      }} catch {{
        showResult(result, '{T("WebUnableToReachServer")}', null);
      }}
    }});
  }});

    loadRoomLinks();
    loadYamlOptions();
    loadAuditEntries();
  }}
</script>
<script>{WebPortalTheme.NavigationScript}</script>
</body>
</html>";
    }
}
