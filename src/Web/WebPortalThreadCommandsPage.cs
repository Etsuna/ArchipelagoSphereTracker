using ArchipelagoSphereTracker.src.Resources;
using System.Text.Json;

public static class WebPortalThreadCommandsPage
{
    public static string Build()
    {
        static string T(string key) => Resource.ResourceManager.GetString(key) ?? key;
        static string Js(string key) => JsonSerializer.Serialize(T(key));

        return @$"<!doctype html>
<html lang=""{Declare.Language}"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>AST · {T("WebRoomPortalTitle")}</title>
  <style>{WebPortalTheme.Styles}</style>
</head>
<body>
  <header>
    <div class=""hero"">
      <div class=""title"">
        <h1>AST · {T("WebRoomPortalTitle")}</h1>
        <span>{T("WebThreadCommandsSubtitle")}</span>
      </div>
      <div class=""badge"">Mode: Thread</div>
    </div>
    <div id=""channel-meta"" class=""meta"">Channel ID: —</div>
    <details class=""hero-details"">
      <summary>{T("WebShowInfo")}</summary>
      <div id=""hero-info"" class=""hero-info"">{T("WebLoadingInfo")}</div>
    </details>
  </header>

  <main class=""management-layout"">
    <section class=""panel"">
      <h2>{T("AstCenterProgress")}</h2>
      <form data-command=""status-games-list"">
        <button type=""submit"">{T("WebShowGameStatus")}</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>{T("WebTrackingHealthAndControls")}</h2>
      <form data-command=""ast-room-health"">
        <button type=""submit"">{T("WebRoomHealth")}</button>
        <div class=""result"" data-result></div>
      </form>
      <form data-command=""ast-sync-now"">
        <button type=""submit"">{T("WebSyncNow")}</button>
        <div class=""result"" data-result></div>
      </form>
      <form data-command=""ast-pause"">
        <button type=""submit"">{T("WebPauseTracking")}</button>
        <div class=""result"" data-result></div>
      </form>
      <form data-command=""ast-resume"">
        <button type=""submit"">{T("WebResumeTracking")}</button>
        <div class=""result"" data-result></div>
      </form>
      <form data-command=""ast-polling"">
        <label>{T("WebPollingMode")}
          <select name=""mode"" required>
            <option value=""automatic"">{T("WebPollingAutomatic")}</option>
            <option value=""fixed"">{T("WebPollingFixedFrequency")}</option>
          </select>
        </label>
        <label>{T("WebPollingMaximumInterval")}
          <select name=""maximumFrequency"" required>
            <option value=""5m"">{T("WebEvery5Minutes")}</option>
            <option value=""15m"">{T("WebEvery15Minutes")}</option>
            <option value=""30m"">{T("WebEvery30Minutes")}</option>
            <option value=""1h"">{T("WebEvery1Hour")}</option>
            <option value=""6h"">{T("WebEvery6Hours")}</option>
            <option value=""12h"">{T("WebEvery12Hours")}</option>
            <option value=""18h"">{T("WebEvery18Hours")}</option>
            <option value=""1d"">{T("WebEveryDay")}</option>
          </select>
        </label>
        <button type=""submit"">{T("WebPollingUpdatePolicy")}</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>{T("AstCenterSpoilerAnalysis")}</h2>
      <form data-command=""send-spoiler-log"">
        <label>{T("SlashImportSpoilerLog")}
          <input type=""file"" name=""file"" accept="".txt,.json"" required />
        </label>
        <button type=""submit"">{T("SlashImportSpoilerLog")}</button>
        <div class=""result"" data-result></div>
      </form>
      <form data-command=""analyze-spoiler-log"">
        <label>{T("AstCenterChooseASlot")}
          <select id=""spoiler-alias-select"" name=""alias""></select>
        </label>
        <label>{T("AstCenterMaximumSphereBlankAll")}
          <input type=""number"" name=""sphere"" min=""0"" />
        </label>
        <label>{T("AstCenterItemDisplay")}
          <select name=""missingMode"">
            <option value=""first"">{T("AstCenterFirstBlockingSphere")}</option>
            <option value=""full"">{T("AstCenterFullReport")}</option>
          </select>
        </label>
        <label>{T("AstCenterHideItems")}
          <select name=""hideItems"">
            <option value=""true"">{T("WebYes")}</option>
            <option value=""false"">{T("WebNo")}</option>
          </select>
        </label>
        <label>{T("AstCenterSphereToValidateOptional")}
          <input type=""number"" name=""validateSphere"" min=""0"" />
        </label>
        <label>{T("AstCenterResetValidation")}
          <select name=""resetValidation"">
            <option value=""false"">{T("WebNo")}</option>
            <option value=""true"">{T("WebYes")}</option>
          </select>
        </label>
        <button type=""submit"">{T("AstCenterAnalyze")}</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>{T("TrackerPatches")}</h2>
      <form id=""patch-form"">
        <label>Alias
          <select id=""patch-alias-select"" name=""alias"" required>
            <option value="""">{T("WebLoadingAliases")}</option>
          </select>
        </label>
        <div class=""result"" id=""patch-link-result""></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>{T("WebCheckFrequency")}</h2>
      <form data-command=""update-frequency-check"">
        <label>{T("WebFrequency")}
          <select name=""checkFrequency"" required>
            <option value=""5m"">{T("WebEvery5Minutes")}</option>
            <option value=""15m"">{T("WebEvery15Minutes")}</option>
            <option value=""30m"">{T("WebEvery30Minutes")}</option>
            <option value=""1h"">{T("WebEvery1Hour")}</option>
            <option value=""6h"">{T("WebEvery6Hours")}</option>
            <option value=""12h"">{T("WebEvery12Hours")}</option>
            <option value=""18h"">{T("WebEvery18Hours")}</option>
            <option value=""1d"">{T("WebEveryDay")}</option>
          </select>
        </label>
        <button type=""submit"">{T("WebUpdateFrequency")}</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>{T("WebSilentMode")}</h2>
      <form data-command=""update-silent-option"">
        <label>{T("WebSilentMode")}
          <select name=""silent"" required>
            <option value=""true"">{T("WebEnabled")}</option>
            <option value=""false"">{T("WebDisabled")}</option>
          </select>
        </label>
        <button type=""submit"">{T("WebUpdateSilentMode")}</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>{T("WebDeleteThreadUrl")}</h2>
      <form data-command=""delete-url"">
        <button type=""submit"">{T("WebDeleteThreadUrl")}</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>{T("AstCenterAssociations")}</h2>
      <form data-command=""get-aliases"">
        <button type=""submit"">{T("AstCenterList")}</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>{T("AstCenterRevokePortal")}</h2>
      <button type=""button"" id=""revoke-portal-button"">{T("AstCenterRevokeLink")}</button>
      <div class=""result"" id=""revoke-portal-result""></div>
    </section>
  </main>

<script>
  const params = new URLSearchParams(window.location.search);
  const m = window.location.pathname.match(/\/portal\/(\d+)\/(\d+)\/([a-f0-9]+)\/thread-commands\.html$/i);

  const guildId = params.get('guildId') || (m ? m[1] : '');
  const channelId = params.get('channelId') || (m ? m[2] : '');
  const token = m ? m[3] : '';

  document.getElementById('channel-meta').textContent = channelId ? ('Channel ID: ' + channelId) : 'Channel ID: —';

  const path = window.location.pathname;
  const idx = path.indexOf('/portal/');
  const basePath = idx >= 0 ? path.substring(0, idx) : '';

  const securedApiBase = window.location.origin + basePath + '/api/portal/' + guildId + '/' + channelId + '/' + token;
  const apiBase = securedApiBase + '/thread-commands/execute';
  const patchAliasesApi = securedApiBase + '/thread-commands/patches';
  const infoApi = securedApiBase + '/info';

  const parsePayload = async (response) => {{
    const raw = await response.text();
    if (!raw) return null;
    try {{return JSON.parse(raw);}} catch {{return raw;}}
 }};

  const extractMessage = (payload, fallback) => {{
    if (payload == null) return fallback;
    if (typeof payload === 'string') return payload || fallback;
    if (typeof payload === 'object' && payload.message) return payload.message;
    return fallback;
 }};

  const formatDiscordStatusMessage = (message) => {{
    if (!message) return '';

    let html = escapeHtml(message);
    html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
    html = html.replace(/~~([^~]+)~~/g, '<s>$1</s>');
    html = html.replace(/\r?\n/g, '<br>');
    return html;
 }};

  const showResult = (container, message, command) => {{
    if (command === 'status-games-list') {{
      container.innerHTML = formatDiscordStatusMessage(message || '');
      return;
    }}

    container.textContent = message || '';
 }};

  const patchAliasSelect = document.getElementById('patch-alias-select');
  const patchLinkResult = document.getElementById('patch-link-result');
  const spoilerAliasSelect = document.getElementById('spoiler-alias-select');
  const revokePortalButton = document.getElementById('revoke-portal-button');
  const revokePortalResult = document.getElementById('revoke-portal-result');
  const heroInfo = document.getElementById('hero-info');
  const patchAliasData = new Map();

  const escapeHtml = (value) => {{
    const div = document.createElement('div');
    div.textContent = value || '';
    return div.innerHTML;
 }};

  const getValidPatchUrl = (value) => {{
    if (!value) return null;
    const trimmed = value.trim();
    if (!trimmed) return null;

    try {{
      const candidate = new URL(trimmed);
      if (candidate.protocol === 'http:' || candidate.protocol === 'https:') return candidate.toString();
      return null;
    }} catch {{
      return null;
    }}
 }};

  const renderPatchForAlias = (alias) => {{
    if (!patchLinkResult) return;

    if (!alias) {{
      patchLinkResult.textContent = {Js("WebSelectAliasForPatch")};
      return;
    }}

    const entry = patchAliasData.get(alias);
    if (!entry) {{
      patchLinkResult.textContent = {Js("WebAliasNotFound")};
      return;
    }}

    const validPatchUrl = getValidPatchUrl(entry.patch);
    const gameLabel = entry.gameName ? ({Js("WebGameLabelPrefix")} + entry.gameName) : {Js("WebGameUnknown")};

    if (validPatchUrl) {{
      patchLinkResult.innerHTML = gameLabel + '<br><a href=""' + escapeHtml(validPatchUrl) + '"" target=""_blank"" rel=""noopener noreferrer"">' + escapeHtml(validPatchUrl) + '</a>';
      return;
    }}

    patchLinkResult.textContent = gameLabel + '\n' + {Js("WebNoPatchLinkForAlias")};
 }};

  const loadPatchAliases = async () => {{
    if (!patchAliasSelect || !patchLinkResult) return;

    if (!guildId || !channelId) {{
      patchAliasSelect.innerHTML = '<option value="""">' + {Js("WebAliasesUnavailable")} + '</option>';
      patchAliasSelect.disabled = true;
      spoilerAliasSelect.innerHTML = '<option value="""">' + {Js("WebAliasesUnavailable")} + '</option>';
      spoilerAliasSelect.disabled = true;
      patchLinkResult.textContent = {Js("WebInvalidUrlMissingIds")};
      return;
    }}

    patchAliasSelect.disabled = true;
    patchAliasSelect.innerHTML = '<option value="""">{T("WebLoadingAliases")}</option>';
    spoilerAliasSelect.disabled = true;
    spoilerAliasSelect.innerHTML = '<option value="""">{T("WebLoadingAliases")}</option>';

    try {{
      const response = await fetch(patchAliasesApi);
      const payload = await parsePayload(response);

      if (!response.ok) {{
        const msg = extractMessage(payload, {Js("WebErrorLoadingAliases")});
        patchAliasSelect.innerHTML = '<option value="""">' + {Js("WebAliasesUnavailable")} + '</option>';
        spoilerAliasSelect.innerHTML = '<option value="""">' + {Js("WebAliasesUnavailable")} + '</option>';
        patchLinkResult.textContent = msg;
        return;
      }}

      const aliases = payload && Array.isArray(payload.aliases) ? payload.aliases : [];
      patchAliasData.clear();

      if (aliases.length === 0) {{
        patchAliasSelect.innerHTML = '<option value="""">' + {Js("WebNoAliasAvailable")} + '</option>';
        spoilerAliasSelect.innerHTML = '<option value="""">' + {Js("WebNoAliasAvailable")} + '</option>';
        patchLinkResult.textContent = {Js("WebNoAliasForThread")};
        return;
      }}

      const options = ['<option value="""">' + {Js("WebSelectAlias")} + '</option>'];
      const spoilerOptions = ['<option value="""">' + {Js("AstCenterAll")} + '</option>'];
      aliases.forEach((entry) => {{
        if (!entry || !entry.alias) return;
        patchAliasData.set(entry.alias, {{ gameName: entry.gameName || '', patch: entry.patch || '' }});
        options.push('<option value=""' + escapeHtml(entry.alias) + '"">' + escapeHtml(entry.alias) + '</option>');
        spoilerOptions.push('<option value=""' + escapeHtml(entry.alias) + '"">' + escapeHtml(entry.alias) + '</option>');
      }});

      patchAliasSelect.innerHTML = options.join('');
      patchAliasSelect.disabled = false;
      spoilerAliasSelect.innerHTML = spoilerOptions.join('');
      spoilerAliasSelect.disabled = false;
      patchLinkResult.textContent = {Js("WebSelectAliasForPatch")};
    }} catch {{
      patchAliasSelect.innerHTML = '<option value="""">' + {Js("WebAliasesUnavailable")} + '</option>';
      spoilerAliasSelect.innerHTML = '<option value="""">' + {Js("WebAliasesUnavailable")} + '</option>';
      patchLinkResult.textContent = {Js("WebUnableToLoadAliases")};
    }}
 }};

  if (patchAliasSelect) {{
    patchAliasSelect.addEventListener('change', () => renderPatchForAlias(patchAliasSelect.value));
 }}

  const linkifyText = (message) => {{
    const safe = escapeHtml(message || '');
    return safe
      .replace(/(https?:\/\/[^\s<]+)/g, '<a href=""$1"" target=""_blank"" rel=""noopener noreferrer"">$1</a>')
      .replace(/\r?\n/g, '<br>');
  }};

  const loadHeroInfo = async () => {{
    if (!heroInfo) return;
    if (!guildId || !channelId) {{
      heroInfo.textContent = {Js("WebInfoUnavailableMissingIds")};
      return;
    }}

    try {{
      const response = await fetch(infoApi);
      const payload = await parsePayload(response);
      const message = extractMessage(payload, response.ok ? '' : {Js("WebUnableToLoadInfo")});
      heroInfo.innerHTML = linkifyText(message || {Js("WebNoInfoAvailable")});
    }} catch {{
      heroInfo.textContent = {Js("WebUnableToLoadInfo")};
    }}
  }};

  loadPatchAliases();
  loadHeroInfo();

  revokePortalButton.addEventListener('click', async () => {{
    if (!window.confirm({Js("WebConfirmRevokePortal")})) return;
    revokePortalResult.textContent = {Js("WebProcessing")};
    try {{
      const response = await fetch(securedApiBase + '/revoke', {{ method: 'POST' }});
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
  }});

  document.querySelectorAll('form[data-command]').forEach((form) => {{
    form.addEventListener('submit', async (event) => {{
      event.preventDefault();
      const result = form.querySelector('[data-result]');

      if (!guildId || !channelId) {{
        showResult(result, {Js("WebInvalidUrlMissingIds")}, form.dataset.command);
        return;
     }}

      if (['ast-pause', 'delete-url'].includes(form.dataset.command) &&
          !window.confirm({Js("WebConfirmDangerousAction")})) {{
        return;
      }}

      const data = new FormData(form);
      data.set('command', form.dataset.command);

      showResult(result, {Js("WebProcessing")}, form.dataset.command);

      try {{
        const response = await fetch(apiBase, {{method: 'POST', body: data}});
        const payload = await parsePayload(response);
        const msg = extractMessage(payload, response.ok ? {Js("WebCommandExecuted")} : {Js("WebCommandError")});
        showResult(result, msg, form.dataset.command);
     }} catch {{
        showResult(result, {Js("WebUnableToReachServer")}, form.dataset.command);
     }}
   }});
 }});
</script>
<script>{WebPortalTheme.NavigationScript}</script>
</body>
</html>";
    }
}
