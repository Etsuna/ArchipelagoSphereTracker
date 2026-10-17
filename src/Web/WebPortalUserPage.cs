using System.Net;
using System.Text.Json;
using ArchipelagoSphereTracker.src.Resources;

public static class WebPortalUserPage
{
    public static string Build(string guildId, string channelId, string token)
    {
        static string T(string key) => Resource.ResourceManager.GetString(key) ?? key;
        static string Js(string key) => JsonSerializer.Serialize(T(key));
        var safeGuildId = WebUtility.HtmlEncode(guildId);
        var safeChannelId = WebUtility.HtmlEncode(channelId);
        var safeToken = WebUtility.HtmlEncode(token);
        var safeApworldInfoUrl = WebUtility.HtmlEncode(Declare.ApworldInfoSheet);

        return $@"<!doctype html>
<html lang=""{Declare.Language}"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>AST · Sphere Tracker</title>
  <style>{WebPortalTheme.Styles}</style>
</head>
<body data-guild=""{safeGuildId}"" data-channel=""{safeChannelId}"" data-token=""{safeToken}"">
  <header>
    <div class=""hero"">
      <div class=""title"">
        <h1 id=""room-title"">AST · Sphere Tracker</h1>
        <span>Archipelago Sphere Tracker · {T("WebPersonalPortal")}</span>
      </div>
    </div>
    <details class=""hero-details"">
      <summary>{T("WebShowInfo")}</summary>
      <div class=""hero-info"" id=""hero-info"">{T("WebLoadingInfo")}</div>
    </details>
  </header>

  <main>
    <section class=""panel toolbar"">
      <h2>{T("WebQuickActions")}</h2>
      <button class=""button"" id=""refresh"">{T("WebRefreshData")}</button>
      <button class=""button danger"" id=""revoke-portal-button"">{T("AstCenterRevokePortal")}</button>
      <div id=""tracking-status"" class=""meta"" role=""status"" aria-live=""polite""></div>
      <div id=""status"" class=""status""></div>
    </section>

    <section class=""panel"" id=""room-overview"">
      <h2>{T("AstCenterProgress")}</h2>
      <div class=""room-stats"">
        <div class=""room-stat""><span>{T("TrackerSlots")}</span><strong id=""stat-slots"">—</strong></div>
        <div class=""room-stat""><span>{T("TrackerGames")}</span><strong id=""stat-games"">—</strong></div>
        <div class=""room-stat""><span>{T("TrackerChecks")}</span><strong id=""stat-checks"">—</strong></div>
        <div class=""room-stat""><span>{T("TrackerProgress")}</span><strong id=""stat-progress"">—</strong></div>
      </div>
      <div class=""tracker-filters"">
        <label>{T("TrackerSearch")}<input id=""slot-search"" type=""search"" placeholder=""{T("TrackerSearchPlaceholder")}"" /></label>
        <label>{T("TrackerGame")}<select id=""slot-game-filter""><option value="""">{T("TrackerAllGames")}</option></select></label>
        <label>{T("TrackerStatus")}<select id=""slot-progress-filter""><option value="""">{T("TrackerAllSlots")}</option><option value=""incomplete"">{T("TrackerIncomplete")}</option><option value=""complete"">{T("TrackerComplete")}</option></select></label>
        <label>{T("TrackerSort")}<select id=""slot-sort""><option value=""name"">{T("TrackerSlot")}</option><option value=""game"">{T("TrackerGame")}</option><option value=""progress-desc"">{T("TrackerMostProgress")}</option><option value=""progress-asc"">{T("TrackerLeastProgress")}</option></select></label>
      </div>
      <div class=""tracker-filter-footer""><label class=""inline-check""><input id=""only-my-slots"" type=""checkbox"" />{T("TrackerOnlyMine")}</label><span id=""slots-shown"" class=""meta"" role=""status""></span></div>
      <div id=""game-status-root""></div>
    </section>

    <details class=""panel"" open>
      <summary><h2>{T("WebCurrentRecap")}</h2></summary>
      <div class=""panel-content"">
        <button class=""button danger"" id=""clear-all-recaps-button"">{T("AstCenterClearAll")}</button>
        <div id=""recap-root"" class=""grid""></div>
      </div>
    </details>

    <details class=""panel"" open>
      <summary><h2>{T("WebActiveHints")}</h2></summary>
      <div id=""hints-root"" class=""grid panel-content""></div>
    </details>

    <details class=""panel"" close>
      <summary><h2>{T("WebReceivedItems")}</h2></summary>
      <div id=""items-root"" class=""grid panel-content""></div>
    </details>

    <details class=""panel"" close>
      <summary><h2>{T("WebYourAliases")}</h2></summary>
      <div class=""actions-grid panel-content"">
        <div class=""action-group"">
          <label for=""add-alias-select"">{T("WebAddExistingAliasInThread")}</label>
          <select id=""add-alias-select""></select>
          <label for=""add-alias-skip-mention-select"">{T("SCAddAliasSkipMention")}</label>
          <select id=""add-alias-skip-mention-select"">
            <option value=""0"">{T("None")}</option>
            <option value=""1"">{T("Filler")}</option>
            <option value=""16"">{T("Trap")}</option>
            <option value=""17"">{T("Filler")} + {T("Trap")}</option>
            <option value=""21"">{T("Filler")} + {T("Trap")} + {T("Useful")}</option>
            <option value=""27"">{T("Filler")} + {T("Trap")} + {T("Useful")} + {T("Required")}</option>
            <option value=""31"">{T("Filler")} + {T("Trap")} + {T("Useful")} + {T("Required")} + {T("Progression")}</option>
        </select>
          <button class=""button"" id=""add-alias-button"">{T("WebAddSelectedAlias")}</button>
        </div>
        <div class=""action-group"">
          <label for=""delete-alias-select"">{T("WebDeleteAliasFromYourList")}</label>
          <select id=""delete-alias-select""></select>
          <button class=""button danger"" id=""delete-alias-button"">{T("WebDeleteSelectedAlias")}</button>
        </div>
        <div class=""action-group companion-group"">
          <label>{T("WebCompanionDescription")}</label>
          <div class=""companion-actions"">
            <button class=""button"" id=""open-companion-button"" disabled>{T("WebOpenCompanion")}</button>
            <button class=""button"" id=""copy-companion-link-button"">{T("WebCopyCompanionLink")}</button>
          </div>
          <span class=""companion-help"" id=""companion-help"">{T("WebCompanionNeedsAlias")}</span>
        </div>
      </div>
    </details>

    <details class=""panel"" close>
      <summary><h2>{T("AstCenterMyPatch")}</h2></summary>
      <div class=""action-group panel-content"">
        <label for=""personal-patch-alias-select"">{T("WebSelectAliasForPatch")}</label>
        <select id=""personal-patch-alias-select""></select>
        <div class=""result"" id=""personal-patch-result""></div>
      </div>
    </details>

    <details class=""panel"" close>
      <summary><h2>{T("AstCenterMyExclusions")}</h2></summary>
      <div class=""actions-grid panel-content"">
        <div class=""action-group"">
          <label for=""exclusion-alias-select"">{T("AstCenterChooseASlot")}</label>
          <select id=""exclusion-alias-select""></select>
          <label for=""available-exclusion-item-select"">{T("AstCenterChooseAnItem")}</label>
          <select id=""available-exclusion-item-select""></select>
          <button class=""button"" id=""add-exclusion-button"">{T("AstCenterAddAnExclusionForSlot")}</button>
        </div>
        <div class=""action-group"">
          <label for=""excluded-item-select"">{T("AstCenterMyExclusions")}</label>
          <select id=""excluded-item-select""></select>
          <button class=""button danger"" id=""delete-exclusion-button"">{T("AstCenterRemoveAnExclusionFromSlot")}</button>
        </div>
      </div>
    </details>

    <details class=""panel"">
      <summary><h2>{T("AstCenterSpoilerAnalysis")}</h2></summary>
      <div class=""actions-grid panel-content"">
        <div class=""result spoiler-status"" id=""spoiler-log-status"">{T("WebCheckingSpoilerLog")}</div>
        <form class=""action-group compact-upload"" id=""spoiler-upload-form"">
          <label>{T("SlashImportSpoilerLog")}</label>
          <input type=""file"" name=""file"" accept="".txt,.json"" required />
          <button class=""button"" type=""submit"">{T("SlashImportSpoilerLog")}</button>
          <div class=""result"" data-result></div>
        </form>
        <form class=""action-group"" id=""spoiler-analysis-form"">
          <label for=""spoiler-alias-select"">{T("AstCenterChooseASlot")}</label>
          <select id=""spoiler-alias-select"" name=""alias""></select>
          <label>{T("AstCenterMaximumSphereBlankAll")}</label>
          <input type=""number"" name=""sphere"" min=""0"" />
          <label>{T("AstCenterItemDisplay")}</label>
          <select name=""missingMode"">
            <option value=""first"">{T("AstCenterFirstBlockingSphere")}</option>
            <option value=""full"">{T("AstCenterFullReport")}</option>
          </select>
          <label>{T("AstCenterHideItems")}</label>
          <select name=""hideItems"">
            <option value=""true"">{T("WebYes")}</option>
            <option value=""false"">{T("WebNo")}</option>
          </select>
          <label>{T("AstCenterSphereToValidateOptional")}</label>
          <input type=""number"" name=""validateSphere"" min=""0"" />
          <label>{T("AstCenterResetValidation")}</label>
          <select name=""resetValidation"">
            <option value=""false"">{T("WebNo")}</option>
            <option value=""true"">{T("WebYes")}</option>
          </select>
          <button class=""button"" type=""submit"">{T("AstCenterAnalyze")}</button>
          <div class=""result"" data-result></div>
        </form>
      </div>
    </details>

    <details class=""panel"" close>
      <summary><h2>{T("AstCenterHelpAndLinks")}</h2></summary>
      <div class=""panel-content"">
        <a href=""{safeApworldInfoUrl}"" target=""_blank"" rel=""noopener noreferrer"">APWorlds</a>
        <a href=""https://discord.gg/PJfWRKVyEW"" target=""_blank"" rel=""noopener noreferrer"">Discord</a>
      </div>
    </details>
  </main>

  <script>
    const ctx = {{
      guildId: document.body.dataset.guild,
      channelId: document.body.dataset.channel,
      token: document.body.dataset.token
    }};

    const status = document.getElementById('status');
    const recapRoot = document.getElementById('recap-root');
    const gameStatusRoot = document.getElementById('game-status-root');
    const itemsRoot = document.getElementById('items-root');
    const hintsRoot = document.getElementById('hints-root');
    const addAliasSelect = document.getElementById('add-alias-select');
    const addAliasSkipMentionSelect = document.getElementById('add-alias-skip-mention-select');
    const deleteAliasSelect = document.getElementById('delete-alias-select');
    const addAliasButton = document.getElementById('add-alias-button');
    const deleteAliasButton = document.getElementById('delete-alias-button');
    const openCompanionButton = document.getElementById('open-companion-button');
    const copyCompanionLinkButton = document.getElementById('copy-companion-link-button');
    const companionHelp = document.getElementById('companion-help');
    const heroInfo = document.getElementById('hero-info');
    const personalPatchAliasSelect = document.getElementById('personal-patch-alias-select');
    const personalPatchResult = document.getElementById('personal-patch-result');
    const exclusionAliasSelect = document.getElementById('exclusion-alias-select');
    const availableExclusionItemSelect = document.getElementById('available-exclusion-item-select');
    const excludedItemSelect = document.getElementById('excluded-item-select');
    const addExclusionButton = document.getElementById('add-exclusion-button');
    const deleteExclusionButton = document.getElementById('delete-exclusion-button');
    const clearAllRecapsButton = document.getElementById('clear-all-recaps-button');
    const revokePortalButton = document.getElementById('revoke-portal-button');
    const spoilerAliasSelect = document.getElementById('spoiler-alias-select');
    const spoilerUploadForm = document.getElementById('spoiler-upload-form');
    const spoilerAnalysisForm = document.getElementById('spoiler-analysis-form');
    const spoilerLogStatus = document.getElementById('spoiler-log-status');
    const personalPatchData = new Map();
    const exclusionData = new Map();
    let companionPortalName = '';

    const path = window.location.pathname; // ex: /AST/portal/g/c/token/
    const idx = path.indexOf('/portal/');
    const basePath = idx >= 0 ? path.substring(0, idx) : '';
    const apiBase = window.location.origin + basePath + '/api/portal/' + ctx.guildId + '/' + ctx.channelId + '/' + ctx.token;
    const infoApi = apiBase + '/info';
    const roomLinksApi = apiBase + '/room-links';

    const escapeHtml = (value) => {{
      const div = document.createElement('div');
      div.textContent = value ?? '';
      return div.innerHTML;
    }};

    const setStatus = (message) => {{
      status.textContent = message;
    }};

    const currentPortalUrl = () => window.location.href.split('#')[0];

    const getValidPatchUrl = (value) => {{
      if (!value) return null;
      try {{
        const candidate = new URL(value.trim());
        return candidate.protocol === 'http:' || candidate.protocol === 'https:'
          ? candidate.toString()
          : null;
      }} catch {{
        return null;
      }}
    }};

    const setCompanionAvailability = (aliases) => {{
      const available = Array.isArray(aliases) && aliases.length > 0;
      openCompanionButton.disabled = !available;
      companionHelp.textContent = available
        ? {Js("WebCompanionReady")}
        : {Js("WebCompanionNeedsAlias")};
    }};

    const linkifyText = (message) => {{
      const safe = escapeHtml(message || '');
      return safe
        .replace(/(https?:\/\/[^\s<]+)/g, '<a href=""$1"" target=""_blank"" rel=""noopener noreferrer"">$1</a>')
        .replace(/\r?\n/g, '<br>');
    }};

    const loadHeroInfo = async () => {{
      if (!heroInfo) return;

      try {{
        const response = await fetch(infoApi);
        const payload = await response.json().catch(() => ({{}}));
        const message = payload && payload.message ? payload.message : (response.ok ? '' : {Js("WebUnableToLoadInfo")});
        heroInfo.innerHTML = linkifyText(message || {Js("WebNoInfoAvailable")});
      }} catch {{
        heroInfo.textContent = {Js("WebUnableToLoadInfo")};
      }}
    }};

    const createAliasCard = (alias, content, actions) => {{
      const card = document.createElement('div');
      card.className = 'alias-card';

      const header = document.createElement('div');
      header.className = 'alias-header';

      const title = document.createElement('h3');
      title.textContent = alias;
      header.appendChild(title);

      if (actions) header.appendChild(actions);

      card.appendChild(header);
      card.appendChild(content);
      return card;
    }};

    let roomSlots = [];
    let mySlotNames = new Set();
    const slotSearch = document.getElementById('slot-search');
    const slotGameFilter = document.getElementById('slot-game-filter');
    const slotProgressFilter = document.getElementById('slot-progress-filter');
    const slotSort = document.getElementById('slot-sort');
    const onlyMySlots = document.getElementById('only-my-slots');
    const numericCount = value => Math.max(0, Number.parseInt(value, 10) || 0);
    const slotProgress = slot => slot.total > 0 ? Math.min(100, slot.checks / slot.total * 100) : 0;
    const slotComplete = slot => slot.total > 0 && slot.checks >= slot.total;

    const renderSlotTable = () => {{
      const search = slotSearch.value.trim().toLocaleLowerCase();
      const slots = roomSlots.filter(slot =>
        (!search || (slot.name + ' ' + slot.game).toLocaleLowerCase().includes(search)) &&
        (!slotGameFilter.value || slot.game === slotGameFilter.value) &&
        (!slotProgressFilter.value || slotComplete(slot) === (slotProgressFilter.value === 'complete')) &&
        (!onlyMySlots.checked || mySlotNames.has(slot.name.toLocaleLowerCase())));
      slots.sort((a, b) => {{
        if (slotSort.value === 'progress-desc') return slotProgress(b) - slotProgress(a) || a.name.localeCompare(b.name);
        if (slotSort.value === 'progress-asc') return slotProgress(a) - slotProgress(b) || a.name.localeCompare(b.name);
        if (slotSort.value === 'game') return a.game.localeCompare(b.game) || a.name.localeCompare(b.name);
        return a.name.localeCompare(b.name);
      }});
      document.getElementById('slots-shown').textContent = {Js("TrackerSlotsShown")}.replace('{{0}}', slots.length).replace('{{1}}', roomSlots.length);
      const table = document.createElement('table');
      table.className = 'tracker-table';
      const caption = document.createElement('caption');
      caption.className = 'sr-only';
      caption.textContent = {Js("AstCenterProgress")};
      table.appendChild(caption);
      const head = table.createTHead().insertRow();
      [{Js("TrackerSlot")}, {Js("TrackerGame")}, {Js("TrackerStatus")}, {Js("TrackerChecks")}, {Js("TrackerActivity")}].forEach(label => {{
        const th = document.createElement('th'); th.scope = 'col'; th.textContent = label; head.appendChild(th);
      }});
      const body = table.createTBody();
      slots.forEach(slot => {{
        const row = body.insertRow();
        row.insertCell().textContent = slot.name;
        row.insertCell().textContent = slot.game;
        const state = document.createElement('span');
        state.className = 'state-label' + (slotComplete(slot) ? ' complete' : '');
        state.textContent = slotComplete(slot) ? {Js("TrackerComplete")} : {Js("TrackerIncomplete")};
        row.insertCell().appendChild(state);
        const checks = row.insertCell(); checks.className = 'checks';
        checks.textContent = slot.checks.toLocaleString() + ' / ' + slot.total.toLocaleString();
        const bar = document.createElement('progress'); bar.max = 100; bar.value = slotProgress(slot);
        bar.setAttribute('aria-label', slot.name + ' · ' + {Js("TrackerChecks")}); checks.appendChild(bar);
        const activity = row.insertCell(); activity.className = 'activity';
        const date = Date.parse(slot.lastActivity);
        activity.textContent = Number.isNaN(date) ? (slot.lastActivity || '—') : new Date(date).toLocaleString();
      }});
      if (!slots.length) {{
        const cell = body.insertRow().insertCell(); cell.colSpan = 5; cell.className = 'empty';
        cell.textContent = roomSlots.length ? {Js("TrackerNoMatches")} : {Js("WebNoInfoAvailable")};
      }}
      const wrap = document.createElement('div'); wrap.className = 'table-wrap'; wrap.appendChild(table);
      gameStatusRoot.replaceChildren(wrap);
    }};
    const renderGameStatuses = (gameStatuses) => {{
      roomSlots = (Array.isArray(gameStatuses) ? gameStatuses : []).map(slot => ({{
        name: slot.name || '?', game: slot.game || '—', checks: numericCount(slot.checks),
        total: numericCount(slot.total), lastActivity: slot.lastActivity || ''
      }}));
      const selectedGame = slotGameFilter.value;
      slotGameFilter.replaceChildren(new Option({Js("TrackerAllGames")}, ''));
      [...new Set(roomSlots.map(slot => slot.game))].sort((a,b) => a.localeCompare(b)).forEach(game => slotGameFilter.add(new Option(game, game)));
      if ([...slotGameFilter.options].some(option => option.value === selectedGame)) slotGameFilter.value = selectedGame;
      const checks = roomSlots.reduce((sum,slot) => sum + slot.checks, 0);
      const total = roomSlots.reduce((sum,slot) => sum + slot.total, 0);
      document.getElementById('stat-slots').textContent = roomSlots.length.toLocaleString();
      document.getElementById('stat-games').textContent = new Set(roomSlots.map(slot => slot.game)).size.toLocaleString();
      document.getElementById('stat-checks').textContent = checks.toLocaleString() + ' / ' + total.toLocaleString();
      document.getElementById('stat-progress').textContent = (total > 0 ? Math.min(100,checks / total * 100) : 0).toLocaleString(undefined, {{maximumFractionDigits:1}}) + '%';
      renderSlotTable();
    }};
    slotSearch.addEventListener('input', renderSlotTable);
    [slotGameFilter, slotProgressFilter, slotSort, onlyMySlots].forEach(control => control.addEventListener('change', renderSlotTable));

    const renderRecaps = (recaps) => {{
      recapRoot.innerHTML = '';
      if (!recaps || recaps.length === 0) {{
        recapRoot.innerHTML = '<p class=""empty"">' + {Js("WebNoActiveRecapForUser")} + '</p>';
        return;
      }}

      recaps.forEach(recap => {{
        const container = document.createElement('div');

        if (!recap.groups || recap.groups.length === 0) {{
          const list = document.createElement('ul');
          list.className = 'list';
          const item = document.createElement('li');
          item.textContent = {Js("WebNoPendingItem")};
          list.appendChild(item);
          container.appendChild(list);
        }} else {{
          recap.groups.forEach(group => {{
            const title = document.createElement('div');
            title.className = 'group-title';
            title.textContent = 'Flag: ' + group.flagLabel;
            container.appendChild(title);

            const list = document.createElement('ul');
            list.className = 'list';

            if (group.items.length === 0) {{
              const item = document.createElement('li');
              item.textContent = {Js("WebNoPendingItem")};
              list.appendChild(item);
            }} else {{
              group.items.forEach(it => {{
                const item = document.createElement('li');
                const suffix = it.count > 1 ? (' ×' + it.count) : '';
                item.innerHTML = '<strong>' + escapeHtml(it.item) + '</strong>' + suffix;

                const badge = document.createElement('span');
                badge.className = 'tag';
                badge.textContent = group.flagLabel;
                item.appendChild(badge);

                list.appendChild(item);
              }});
            }}

            container.appendChild(list);
          }});
        }}

        const actions = document.createElement('button');
        actions.className = 'button danger';
        actions.textContent = {Js("WebDeleteRecap")};
        actions.addEventListener('click', () => deleteRecap(recap.alias));

        recapRoot.appendChild(createAliasCard(recap.alias, container, actions));
      }});
    }};

    const renderItems = (items) => {{
      itemsRoot.innerHTML = '';
      if (!items || items.length === 0) {{
        itemsRoot.innerHTML = '<p class=""empty"">' + {Js("WebNoReceivedItemsYet")} + '</p>';
        return;
      }}

      items.forEach(group => {{
        const container = document.createElement('div');

        if (!group.groups || group.groups.length === 0) {{
          const list = document.createElement('ul');
          list.className = 'list';
          const entry = document.createElement('li');
          entry.textContent = {Js("WebNoReceivedItems")};
          list.appendChild(entry);
          container.appendChild(list);
        }} else {{
          group.groups.forEach(flagGroup => {{
            const title = document.createElement('div');
            title.className = 'group-title';
            title.textContent = 'Flag: ' + flagGroup.flagLabel;
            container.appendChild(title);

            const list = document.createElement('ul');
            list.className = 'list';

            if (flagGroup.items.length === 0) {{
              const entry = document.createElement('li');
              entry.textContent = {Js("WebNoReceivedItems")};
              list.appendChild(entry);
            }} else {{
              flagGroup.items.forEach(it => {{
                const entry = document.createElement('li');
                entry.innerHTML =
                  '<strong>' + escapeHtml(it.item) + '</strong> ' +
                  '<span class=""meta"">(' + escapeHtml(it.game) + ')</span>';

                const detail = document.createElement('div');
                detail.className = 'meta';
                detail.textContent = 'Finder: ' + it.finder + ' · Location: ' + it.location;
                entry.appendChild(detail);

                const badge = document.createElement('span');
                badge.className = 'tag';
                badge.textContent = flagGroup.flagLabel;
                entry.appendChild(badge);

                list.appendChild(entry);
              }});
            }}

            container.appendChild(list);
          }});
        }}

        itemsRoot.appendChild(createAliasCard(group.alias, container));
      }});
    }};

    const makeHintMeta = (hint, isReceiver) => {{
      const meta = document.createElement('div');
      meta.className = 'meta';
      meta.textContent = isReceiver
        ? ('Finder: ' + hint.finder + ' · Game: ' + hint.game)
        : ('Receiver: ' + hint.receiver + ' · Game: ' + hint.game);
      return meta;
    }};

    const renderHints = (hints) => {{
      hintsRoot.innerHTML = '';
      if (!hints || hints.length === 0) {{
        hintsRoot.innerHTML = '<p class=""empty"">' + {Js("WebNoActiveHint")} + '</p>';
        return;
      }}

      hints.forEach(group => {{
        const wrapper = document.createElement('div');
        wrapper.className = 'grid';

        const receiverList = document.createElement('ul');
        receiverList.className = 'list';
        if (group.asReceiver.length === 0) {{
          const entry = document.createElement('li');
          entry.textContent = {Js("WebNoHintAsReceiver")};
          receiverList.appendChild(entry);
        }} else {{
          group.asReceiver.forEach(hint => {{
            const entry = document.createElement('li');
            entry.innerHTML =
              '<strong>' + escapeHtml(hint.item) + '</strong> ' +
              '<span class=""meta"">@' + escapeHtml(hint.location) + '</span>';
            entry.appendChild(makeHintMeta(hint, true));
            receiverList.appendChild(entry);
          }});
        }}

        const finderList = document.createElement('ul');
        finderList.className = 'list';
        if (group.asFinder.length === 0) {{
          const entry = document.createElement('li');
          entry.textContent = {Js("WebNoHintAsFinder")};
          finderList.appendChild(entry);
        }} else {{
          group.asFinder.forEach(hint => {{
            const entry = document.createElement('li');
            entry.innerHTML =
              '<strong>' + escapeHtml(hint.item) + '</strong> ' +
              '<span class=""meta"">@' + escapeHtml(hint.location) + '</span>';
            entry.appendChild(makeHintMeta(hint, false));
            finderList.appendChild(entry);
          }});
        }}

        const receiverBlock = document.createElement('div');
        receiverBlock.appendChild(document.createElement('h4')).textContent = 'Receiver';
        receiverBlock.appendChild(receiverList);

        const finderBlock = document.createElement('div');
        finderBlock.appendChild(document.createElement('h4')).textContent = 'Finder';
        finderBlock.appendChild(finderList);

        wrapper.appendChild(receiverBlock);
        wrapper.appendChild(finderBlock);

        hintsRoot.appendChild(createAliasCard(group.alias, wrapper));
      }});
    }};

    const deleteRecap = async (alias) => {{
      if (!window.confirm({Js("WebConfirmDeleteRecap")})) return;
      setStatus({Js("WebDeletingRecap")});
      const formData = new FormData();
      formData.append('alias', alias);

      const res = await fetch(apiBase + '/recap/delete', {{
        method: 'POST',
        body: formData
      }});

      if (res.ok) {{
        setStatus({Js("WebRecapDeletedFor")} + ' ' + alias + '.');
        await loadData();
      }} else {{
        setStatus({Js("WebUnableToDeleteRecap")});
      }}
    }};

    const clearAllRecaps = async () => {{
      if (!window.confirm({Js("WebConfirmClearAllRecaps")})) return;
      setStatus({Js("WebDeletingRecap")});
      const response = await fetch(apiBase + '/recap/delete-all', {{ method: 'POST' }});
      const payload = await response.json().catch(() => ({{}}));
      setStatus(payload.message || (response.ok ? {Js("WebCommandExecuted")} : {Js("WebUnableToDeleteRecap")}));
      if (response.ok) await loadData();
    }};

    const renderPersonalPatch = () => {{
      const entry = personalPatchData.get(personalPatchAliasSelect.value);
      if (!entry) {{
        personalPatchResult.textContent = {Js("WebSelectAliasForPatch")};
        return;
      }}

      const gameLabel = entry.gameName
        ? ({Js("WebGameLabelPrefix")} + entry.gameName)
        : {Js("WebGameUnknown")};
      const patchUrl = getValidPatchUrl(entry.patch);
      if (!patchUrl) {{
        personalPatchResult.textContent = gameLabel + '\n' + {Js("WebNoPatchLinkForAlias")};
        return;
      }}

      personalPatchResult.innerHTML = gameLabel + '<br><a href=""' + escapeHtml(patchUrl) + '"" target=""_blank"" rel=""noopener noreferrer"">' + escapeHtml(patchUrl) + '</a>';
    }};

    const loadPersonalPatches = async () => {{
      personalPatchAliasSelect.disabled = true;
      personalPatchAliasSelect.innerHTML = '<option value="""">' + {Js("WebLoadingAliases")} + '</option>';
      try {{
        const response = await fetch(apiBase + '/patches');
        const payload = await response.json().catch(() => ({{}}));
        const aliases = response.ok && Array.isArray(payload.aliases) ? payload.aliases : [];
        personalPatchData.clear();
        aliases.forEach(entry => {{
          if (entry && entry.alias) personalPatchData.set(entry.alias, entry);
        }});
        fillSelect(
          personalPatchAliasSelect,
          Array.from(personalPatchData.keys()),
          {Js("AstCenterNoAssociatedSlot")},
          {Js("WebSelectAlias")});
        personalPatchAliasSelect.disabled = aliases.length === 0;
        renderPersonalPatch();
      }} catch {{
        fillSelect(personalPatchAliasSelect, [], {Js("WebUnableToLoadAliases")}, {Js("WebSelectAlias")});
        personalPatchResult.textContent = {Js("WebUnableToLoadAliases")};
      }}
    }};

    const fillItemSelect = (select, values, emptyLabel, placeholder) => {{
      const safeValues = Array.isArray(values) ? values : [];
      if (safeValues.length === 0) {{
        select.innerHTML = '<option value="""">' + emptyLabel + '</option>';
        select.disabled = true;
        return;
      }}

      select.innerHTML = ['<option value="""">' + placeholder + '</option>']
        .concat(safeValues.map(value => '<option value=""' + escapeHtml(value) + '"">' + escapeHtml(value) + '</option>'))
        .join('');
      select.disabled = false;
    }};

    const renderExcludedItems = () => {{
      const entry = exclusionData.get(exclusionAliasSelect.value);
      fillItemSelect(
        excludedItemSelect,
        entry ? entry.items : [],
        {Js("AstCenterNoPersonalExclusion")},
        {Js("AstCenterChooseAnItem")});
    }};

    const loadAvailableExclusionItems = async () => {{
      const alias = exclusionAliasSelect.value;
      if (!alias) {{
        fillItemSelect(availableExclusionItemSelect, [], {Js("AstCenterNoAssociatedSlot")}, {Js("AstCenterChooseAnItem")});
        renderExcludedItems();
        return;
      }}

      availableExclusionItemSelect.disabled = true;
      availableExclusionItemSelect.innerHTML = '<option value="""">' + {Js("WebProcessing")} + '</option>';
      try {{
        const response = await fetch(apiBase + '/exclusions/items?alias=' + encodeURIComponent(alias));
        const payload = await response.json().catch(() => ({{}}));
        fillItemSelect(
          availableExclusionItemSelect,
          response.ok ? payload.items : [],
          {Js("WebNoAvailableItem")},
          {Js("AstCenterChooseAnItem")});
      }} catch {{
        fillItemSelect(availableExclusionItemSelect, [], {Js("WebNoAvailableItem")}, {Js("AstCenterChooseAnItem")});
      }}
      renderExcludedItems();
    }};

    const loadExclusions = async () => {{
      exclusionAliasSelect.disabled = true;
      exclusionAliasSelect.innerHTML = '<option value="""">' + {Js("WebLoadingYourAliases")} + '</option>';
      try {{
        const response = await fetch(apiBase + '/exclusions');
        const payload = await response.json().catch(() => ({{}}));
        const aliases = response.ok && Array.isArray(payload.aliases) ? payload.aliases : [];
        exclusionData.clear();
        aliases.forEach(entry => {{
          if (entry && entry.alias) exclusionData.set(entry.alias, entry);
        }});
        fillSelect(
          exclusionAliasSelect,
          Array.from(exclusionData.keys()),
          {Js("AstCenterNoAssociatedSlot")},
          {Js("AstCenterChooseASlot")});
        exclusionAliasSelect.disabled = aliases.length === 0;
        await loadAvailableExclusionItems();
      }} catch {{
        fillSelect(exclusionAliasSelect, [], {Js("WebUnableToLoadYourAliases")}, {Js("AstCenterChooseASlot")});
      }}
    }};

    const mutateExclusion = async (action) => {{
      const alias = exclusionAliasSelect.value;
      const item = action === 'add' ? availableExclusionItemSelect.value : excludedItemSelect.value;
      if (!alias || !item) {{
        setStatus({Js("WebSelectAliasAndItem")});
        return;
      }}
      if (action === 'delete' && !window.confirm({Js("WebConfirmDeleteExclusion")})) return;

      const data = new FormData();
      data.append('alias', alias);
      data.append('item', item);
      const response = await fetch(apiBase + '/exclusion/' + action, {{ method: 'POST', body: data }});
      const payload = await response.json().catch(() => ({{}}));
      setStatus(payload.message || (response.ok ? {Js("WebCommandExecuted")} : {Js("WebCommandError")}));
      if (response.ok) await loadExclusions();
    }};

    const revokePortal = async () => {{
      if (!window.confirm({Js("WebConfirmRevokePortal")})) return;
      const response = await fetch(apiBase + '/revoke', {{ method: 'POST' }});
      const payload = await response.json().catch(() => ({{}}));
      setStatus(payload.message || (response.ok ? {Js("AstCenterThePortalLinkWasRevoked")} : {Js("WebCommandError")}));
      if (response.ok) {{
        document.querySelectorAll('button, select').forEach(element => {{ element.disabled = true; }});
      }}
    }};

    const fillSelect = (select, aliases, emptyLabel, placeholder) => {{
      if (!aliases || aliases.length === 0) {{
        select.innerHTML = '<option value="""">' + emptyLabel + '</option>';
        return;
      }}

      const options = ['<option value="""">' + placeholder + '</option>'];
      aliases.forEach(alias => {{
        options.push('<option value=""' + escapeHtml(alias) + '"">' + escapeHtml(alias) + '</option>');
      }});
      select.innerHTML = options.join('');
    }};

    const fillSpoilerAliases = (aliases) => {{
      const options = ['<option value="""">' + {Js("AstCenterAll")} + '</option>'];
      (aliases || []).forEach(alias => {{
        options.push('<option value=""' + escapeHtml(alias) + '"">' + escapeHtml(alias) + '</option>');
      }});
      spoilerAliasSelect.innerHTML = options.join('');
      spoilerAliasSelect.disabled = false;
    }};

    const loadSpoilerStatus = async () => {{
      spoilerLogStatus.textContent = {Js("WebCheckingSpoilerLog")};
      try {{
        const response = await fetch(apiBase + '/spoiler/status');
        const payload = await response.json().catch(() => ({{}}));
        if (response.ok && payload.exists) {{
          spoilerLogStatus.textContent = {Js("WebSpoilerLogAlreadyImported")}.replace('{{0}}', payload.fileName || '?');
        }} else if (response.ok) {{
          spoilerLogStatus.textContent = {Js("WebNoSpoilerLogImported")};
        }} else {{
          spoilerLogStatus.textContent = payload.message || {Js("WebCommandError")};
        }}
      }} catch {{
        spoilerLogStatus.textContent = {Js("WebCommandError")};
      }}
    }};

    const executeSpoilerAction = async (form, endpoint) => {{
      const result = form.querySelector('[data-result]');
      const button = form.querySelector('button[type=""submit""]');
      button.disabled = true;
      result.textContent = {Js("WebProcessing")};
      try {{
        const response = await fetch(apiBase + endpoint, {{ method: 'POST', body: new FormData(form) }});
        const payload = await response.json().catch(() => ({{}}));
        result.textContent = payload.message || (response.ok ? {Js("WebCommandExecuted")} : {Js("WebCommandError")});
        if (response.ok && endpoint === '/spoiler/upload') {{
          form.reset();
          await loadSpoilerStatus();
        }}
      }} catch {{
        result.textContent = {Js("WebCommandError")};
      }} finally {{
        button.disabled = false;
      }}
    }};

    const loadAliasLists = async () => {{
      addAliasSelect.innerHTML = '<option value="""">' + {Js("WebLoadingThreadAliases")} + '</option>';
      deleteAliasSelect.innerHTML = '<option value="""">' + {Js("WebLoadingYourAliases")} + '</option>';

      try {{
        const [allRes, userRes] = await Promise.all([
          fetch(apiBase + '/aliases'),
          fetch(apiBase + '/aliases/user')
        ]);

        if (allRes.ok) {{
          const payload = await allRes.json();
          const aliases = payload.aliases || [];
          fillSelect(addAliasSelect, aliases, {Js("WebNoAliasInThread")}, {Js("WebSelectAlias")});
          fillSpoilerAliases(aliases);
        }} else {{
          fillSelect(addAliasSelect, [], {Js("WebUnableToLoadThreadAliases")}, {Js("WebSelectAlias")});
          fillSpoilerAliases([]);
        }}

        if (userRes.ok) {{
          const payload = await userRes.json();
          const userAliases = payload.aliases || [];
          fillSelect(deleteAliasSelect, userAliases, {Js("WebNoRegisteredAlias")}, {Js("WebSelectAlias")});
          setCompanionAvailability(userAliases);
        }} else {{
          fillSelect(deleteAliasSelect, [], {Js("WebUnableToLoadYourAliases")}, {Js("WebSelectAlias")});
          setCompanionAvailability([]);
        }}
      }} catch (e) {{
        fillSelect(addAliasSelect, [], {Js("WebUnableToLoadThreadAliases")}, {Js("WebSelectAlias")});
        fillSelect(deleteAliasSelect, [], {Js("WebUnableToLoadYourAliases")}, {Js("WebSelectAlias")});
        fillSpoilerAliases([]);
        setCompanionAvailability([]);
      }}
    }};

    const loadPortalThreadName = async () => {{
      try {{
        const response = await fetch(roomLinksApi);
        if (!response.ok) return;
        const payload = await response.json();
        const link = (payload.links || []).find(item => String(item.channelId) === String(ctx.channelId));
        companionPortalName = link && link.threadName ? link.threadName : '';
        if (companionPortalName) document.getElementById('room-title').textContent = companionPortalName;
      }} catch {{
        companionPortalName = '';
      }}
    }};

    const openInCompanion = async () => {{
      if (openCompanionButton.disabled) return;
      if (!companionPortalName) await loadPortalThreadName();
      const namePart = companionPortalName ? '&name=' + encodeURIComponent(companionPortalName) : '';
      const deepLink = 'ast-companion://connect?portal=' + encodeURIComponent(currentPortalUrl()) + namePart;
      setStatus({Js("WebCompanionOpening")});
      window.location.href = deepLink;
    }};

    const copyCompanionLink = async () => {{
      try {{
        await navigator.clipboard.writeText(currentPortalUrl());
        setStatus({Js("WebCompanionLinkCopied")});
      }} catch {{
        setStatus({Js("WebCompanionCopyFailed")});
      }}
    }};

    const addAliasFromPortal = async () => {{
      const alias = addAliasSelect.value;
      const skipMention = addAliasSkipMentionSelect?.value || '0';      
      if (!alias) {{
        setStatus({Js("WebSelectAliasToAdd")});
        return;
      }}

      setStatus({Js("WebAddingAlias")});
      const formData = new FormData();
      formData.append('alias', alias);
      formData.append('skipMention', skipMention);

      const res = await fetch(apiBase + '/alias/add', {{ method: 'POST', body: formData }});
      if (res.ok) {{
        setStatus({Js("WebAliasAddedPrefix")} + ' ' + alias + '.');
        await loadData();
        return;
      }}

      const payload = await res.json().catch(() => ({{}}));
      setStatus(payload.message || {Js("WebUnableToAddAlias")});
    }};

    const deleteAliasFromPortal = async () => {{
      const alias = deleteAliasSelect.value;
      if (!alias) {{
        setStatus({Js("WebSelectAliasToDelete")});
        return;
      }}

      setStatus({Js("WebDeletingAlias")});
      const formData = new FormData();
      formData.append('alias', alias);

      const res = await fetch(apiBase + '/alias/delete', {{ method: 'POST', body: formData }});
      if (res.ok) {{
        setStatus({Js("WebAliasDeletedPrefix")} + ' ' + alias + '.');
        await loadData();
        return;
      }}

      const payload = await res.json().catch(() => ({{}}));
      setStatus(payload.message || {Js("WebUnableToDeleteAlias")});
    }};

    let summaryLoading = false;
    let refreshTimer;
    let pendingRefreshPolls = 0;
    const refreshButton = document.getElementById('refresh');
    const trackingStatus = document.getElementById('tracking-status');
    const loadData = async () => {{
      if (summaryLoading) return;
      clearTimeout(refreshTimer);
      summaryLoading = true;
      refreshButton.disabled = true;
      let nextRefreshDelay = 60000;
      try {{
        const res = await fetch(apiBase + '/summary', {{ cache: 'no-store' }});
        if (!res.ok) {{
          setStatus({Js("WebPortalUnavailable")});
          // A revoked or expired link should not keep making background requests.
          if (res.status === 401 || res.status === 403 || res.status === 404) nextRefreshDelay = 0;
          return;
        }}
        const data = await res.json();
        setStatus('');
        const tracking = data.tracking || {{ isUnavailable: true }};
        let freshness = {Js("WebLastArchipelagoSync")} + ': ' +
          (tracking.lastSuccessAtUtc ? new Date(tracking.lastSuccessAtUtc).toLocaleString() : {Js("WebSyncNever")});
        if (tracking.isPaused) freshness += ' · ' + {Js("WebSyncPaused")};
        else if (tracking.isUnavailable) freshness += ' · ' + {Js("WebSyncUnavailable")};
        else if (tracking.isSyncing) freshness += ' · ' + {Js("WebSyncPending")};
        else if (tracking.lastFailureKind && tracking.lastFailureKind !== 'None') freshness += ' · ' + {Js("WebSyncFailed")};
        trackingStatus.textContent = freshness;
        if (tracking.isSyncing && pendingRefreshPolls < 4) {{
          pendingRefreshPolls++;
          nextRefreshDelay = 5000;
        }} else if (!tracking.isSyncing) pendingRefreshPolls = 0;
        mySlotNames = new Set((data.receivedItems || []).map(slot => (slot.alias || '').toLocaleLowerCase()));
        renderGameStatuses(data.gameStatuses || []);
        renderRecaps(data.recaps || []);
        renderItems(data.receivedItems || []);
        renderHints(data.hints || []);
        await Promise.all([loadAliasLists(), loadPersonalPatches(), loadExclusions(), loadSpoilerStatus()]);
      }} catch {{
        setStatus({Js("WebPortalUnavailable")});
      }} finally {{
        summaryLoading = false;
        refreshButton.disabled = false;
        if (nextRefreshDelay > 0) refreshTimer = setTimeout(() => {{
          if (document.visibilityState === 'visible') loadData();
        }}, nextRefreshDelay);
      }}
    }};

    document.addEventListener('visibilitychange', () => {{
      if (document.visibilityState === 'visible') loadData();
      else clearTimeout(refreshTimer);
    }});

    document.getElementById('refresh').addEventListener('click', loadData);
    addAliasButton.addEventListener('click', addAliasFromPortal);
    deleteAliasButton.addEventListener('click', deleteAliasFromPortal);
    openCompanionButton.addEventListener('click', openInCompanion);
    copyCompanionLinkButton.addEventListener('click', copyCompanionLink);
    personalPatchAliasSelect.addEventListener('change', renderPersonalPatch);
    exclusionAliasSelect.addEventListener('change', loadAvailableExclusionItems);
    addExclusionButton.addEventListener('click', () => mutateExclusion('add'));
    deleteExclusionButton.addEventListener('click', () => mutateExclusion('delete'));
    clearAllRecapsButton.addEventListener('click', clearAllRecaps);
    revokePortalButton.addEventListener('click', revokePortal);
    spoilerUploadForm.addEventListener('submit', event => {{
      event.preventDefault();
      executeSpoilerAction(spoilerUploadForm, '/spoiler/upload');
    }});
    spoilerAnalysisForm.addEventListener('submit', event => {{
      event.preventDefault();
      executeSpoilerAction(spoilerAnalysisForm, '/spoiler/analyze');
    }});
    loadHeroInfo();
    loadPortalThreadName();
    loadData();
  </script>
<script>{WebPortalTheme.NavigationScript}</script>
</body>
</html>";
    }
}
