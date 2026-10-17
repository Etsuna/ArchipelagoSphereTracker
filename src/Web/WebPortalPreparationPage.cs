using System.Text.Json;
using ArchipelagoSphereTracker.src.Resources;

public static class WebPortalPreparationPage
{
    public static string Build()
    {
        static string T(string key) => Resource.ResourceManager.GetString(key) ?? key;
        static string Js(string key) => JsonSerializer.Serialize(T(key));
        return $@"<!doctype html>
<html lang=""{Declare.Language}""><head><meta charset=""utf-8"" /><meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
<title>AST · {T("PrepTitle")}</title><style>{WebPortalTheme.Styles}
pre {{ white-space:pre-wrap; overflow-wrap:anywhere; }} .prep-slots {{ color:var(--muted); font-size:12px; }}
.prep-files {{ display:grid; gap:12px; }} .prep-file {{ border:1px solid var(--line); padding:12px; border-radius:5px; }}
</style></head><body>
<header><div class=""hero""><div class=""title""><h1 id=""prep-title"">{T("PrepTitle")}</h1><span>Archipelago Sphere Tracker</span></div><span class=""badge"" id=""prep-state""></span></div></header>
<main>
<section class=""panel toolbar""><h2>{T("PrepRefresh")}</h2><button id=""refresh"">{T("PrepRefresh")}</button><div class=""status"" id=""status"" role=""status"" aria-live=""polite""></div></section>
<section class=""panel""><h2>{T("PrepPlayers")}</h2><p id=""prep-summary"" class=""meta""></p>
<div class=""toolbar""><button data-action=""join"">{T("PrepJoin")}</button><button data-action=""ready"" id=""ready"">{T("PrepReady")}</button><button data-action=""leave"">{T("PrepLeave")}</button></div>
<p class=""meta"">{T("PrepInstructions")}</p><div class=""table-wrap""><table class=""tracker-table""><thead><tr><th scope=""col"">{T("PrepPlayer")}</th><th scope=""col"">{T("PrepSlots")}</th><th scope=""col"">YAML</th><th scope=""col"">{T("PrepReadiness")}</th></tr></thead><tbody id=""players""></tbody></table></div>
<p class=""status"" id=""prep-error""></p><p class=""meta"" id=""prep-validation""></p></section>
<section class=""panel""><h2>{T("PrepFiles")}</h2><p class=""meta"">{T("PrepValidationNote")}</p>
<form id=""yaml-form""><label>{T("PrepUpload")}<input type=""file"" name=""file"" accept="".yaml,.yml"" required /></label><button type=""submit"">{T("PrepUpload")}</button></form><div class=""prep-files"" id=""files""></div></section>
<section class=""panel"" id=""organizer"" hidden><h2>{T("PrepManage")}</h2><div class=""toolbar""><button data-action=""close"">{T("PrepClose")}</button><button data-action=""reopen"">{T("PrepReopen")}</button><button data-action=""test"">{T("PrepTest")}</button><button data-action=""generate"">{T("PrepGenerate")}</button><button data-action=""cancel"" class=""danger"">{T("PrepCancel")}</button></div>
<label class=""inline-check""><input type=""checkbox"" id=""skip-balancing"" />{T("PrepSkipBalancing")}</label>
<form id=""invite-form""><label>{T("PrepInviteId")}<input name=""userId"" pattern=""[0-9]+"" required /></label><button type=""submit"">{T("PrepInvite")}</button></form>
<form id=""attach-form"" hidden><label>{T("PrepRoomUrl")}<input type=""url"" name=""url"" placeholder=""https://archipelago.gg/room/…"" required /></label><button type=""submit"">{T("PrepAttach")}</button></form></section>
<section class=""panel"" id=""generation-result"" hidden><h2>{T("PrepStateGenerated")}</h2><p>{T("PrepGenerated")}</p><a id=""archive"">{T("PrepDownload")}</a><p id=""tracking""></p></section>
</main><script>
const path = window.location.pathname;
const portalIndex = path.indexOf('/portal/');
const segments = path.substring(portalIndex + 8).split('/');
const apiBase = window.location.origin + path.substring(0, portalIndex) + '/api/portal/' + segments.slice(0,3).join('/') + '/preparation';
const status = document.getElementById('status');
let current = null, busy = false, timer = null, actionBusy = false;
const element = (tag, text) => {{ const node = document.createElement(tag); node.textContent = text; return node; }};
const render = view => {{
  current = view;
  document.getElementById('prep-title').textContent = view.title;
  document.getElementById('prep-state').textContent = view.stateLabel;
  document.getElementById('prep-summary').textContent = view.summary;
  document.getElementById('prep-error').textContent = view.error || '';
  document.getElementById('prep-validation').textContent = view.testPassed ? {Js("PrepTestPassed")} : '';
  document.getElementById('organizer').hidden = !view.canManage;
  document.getElementById('yaml-form').hidden = !view.canEdit || !view.isMember;
  document.getElementById('attach-form').hidden = !view.canManage || view.state !== 'Generated' || !!view.trackingChannelId;
  document.getElementById('invite-form').hidden = !view.canInvite || view.state !== 'Open';
  document.getElementById('generation-result').hidden = view.state !== 'Generated' && view.state !== 'Attaching';
  const archive = document.getElementById('archive'); archive.hidden = !view.canDownload; archive.href = apiBase + '/download';
  const tracking = document.getElementById('tracking'); tracking.replaceChildren();
  if (view.trackingChannelId) {{
    const link = element('a', {Js("PrepTracking")});
    link.href = 'https://discord.com/channels/' + encodeURIComponent(segments[0]) + '/' + encodeURIComponent(view.trackingChannelId);
    link.target = '_blank'; link.rel = 'noopener noreferrer'; tracking.appendChild(link);
  }}
  const players = document.getElementById('players'); players.replaceChildren();
  view.participants.forEach(player => {{
    const row = document.createElement('tr'); row.appendChild(element('td', player.name));
    const slots = element('td','');
    player.slots.forEach(slot => slots.appendChild(element('div', slot.name + ' · ' + slot.game)));
    row.appendChild(slots); row.appendChild(element('td', player.validation));
    const ready = element('span', player.ready ? {Js("PrepReady")} : {Js("PrepNotReady")}); ready.className = 'state-label' + (player.ready ? ' complete' : '');
    const cell = element('td',''); cell.appendChild(ready); row.appendChild(cell); players.appendChild(row);
  }});
  if (!view.participants.length) {{ const row = document.createElement('tr'); const cell = element('td', {Js("PrepNoPlayers")}); cell.colSpan = 4; row.appendChild(cell); players.appendChild(row); }}
  const files = document.getElementById('files'); files.replaceChildren();
  view.files.forEach(file => {{
    const card = element('div',''); card.className = 'prep-file'; card.appendChild(element('strong', file.fileName));
    card.appendChild(element('p', file.validation));
    const download = element('a', {Js("PrepDownloadYaml")}); download.href = apiBase + '/files/' + encodeURIComponent(file.id); card.appendChild(download);
    if (file.error) card.appendChild(element('pre', file.error));
    if (view.canEdit) {{ const remove = element('button', {Js("PrepRemove")}); remove.type = 'button'; remove.addEventListener('click', () => {{ const data = new FormData(); data.set('fileId',file.id); act('remove', data); }}); card.appendChild(remove); }}
    files.appendChild(card);
  }});
  if (!view.files.length) files.appendChild(element('p', {Js("PrepNoFiles")}));
  document.querySelectorAll('[data-action]').forEach(button => {{
    const action = button.dataset.action;
    if (action === 'join') button.disabled = view.isMember || view.state !== 'Open';
    else if (action === 'ready' || action === 'not-ready') {{ button.dataset.action = view.meReady ? 'not-ready' : 'ready'; button.textContent = view.meReady ? {Js("PrepNotReady")} : {Js("PrepReady")}; button.disabled = !view.isMember || !view.canEdit || (!view.meReady && !view.canReady); }}
    else if (action === 'leave') button.disabled = !view.isMember || !view.canEdit;
    else if (action === 'generate') button.disabled = !view.canGenerate;
    else if (action === 'test') button.disabled = !view.canTest;
    else if (action === 'close') button.disabled = view.state !== 'Open';
    else if (action === 'reopen') button.disabled = view.state !== 'Closed';
    else button.disabled = !view.canEdit;
  }});
}};
const load = async () => {{
  if (busy || actionBusy) return;
  clearTimeout(timer); busy = true; let valid = true;
  try {{
    const response = await fetch(apiBase, {{cache:'no-store'}});
    if (!response.ok) {{ valid = ![401,403,404].includes(response.status); throw new Error({Js("WebPortalUnavailable")}); }}
    render(await response.json());
  }} catch(error) {{ status.textContent = error.message; }} finally {{
    busy = false;
    if (valid) timer = setTimeout(() => {{ if (document.visibilityState === 'visible') load(); }}, current && (['Validating','Generating','Attaching'].includes(current.state) || current.files.some(file => file.pending)) ? 5000 : 15000);
  }}
}};
const act = async (action, data = new FormData()) => {{
  if (actionBusy) return;
  if (action === 'cancel' && !confirm({Js("PrepCancelConfirm")})) return;
  if (action === 'generate' && !confirm({Js("PrepGenerateConfirm")})) return;
  data.set('skipBalancing', String(document.getElementById('skip-balancing').checked));
  actionBusy = true; clearTimeout(timer);
  try {{
    const response = await fetch(apiBase + '/' + encodeURIComponent(action), {{method:'POST',body:data}});
    const result = await response.json(); status.textContent = result.message || {Js("PrepSaved")};
  }} catch {{ status.textContent = {Js("WebUnableToReachServer")}; }}
  finally {{ actionBusy = false; await load(); }}
}};
document.querySelectorAll('[data-action]').forEach(button => button.addEventListener('click', () => act(button.dataset.action)));
document.getElementById('yaml-form').addEventListener('submit', async event => {{ event.preventDefault(); await act('upload',new FormData(event.currentTarget)); }});
document.getElementById('attach-form').addEventListener('submit', async event => {{ event.preventDefault(); await act('attach',new FormData(event.currentTarget)); }});
document.getElementById('invite-form').addEventListener('submit', async event => {{ event.preventDefault(); await act('invite',new FormData(event.currentTarget)); }});
document.getElementById('refresh').addEventListener('click',load);
document.addEventListener('visibilitychange', () => {{ if (document.visibilityState === 'visible') load(); else clearTimeout(timer); }});
load();
</script><script>{WebPortalTheme.NavigationScript}</script></body></html>";
    }
}
