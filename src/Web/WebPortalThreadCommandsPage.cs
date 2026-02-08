public static class WebPortalThreadCommandsPage
{
    public static string Build()
    {
        return @$"<!doctype html>
<html lang=""fr"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>AST Room Portal</title>
  <style>
    :root {{
      color-scheme: dark;
      --bg: #0b0f1f;
      --bg-2: #13172b;
      --panel: rgba(18, 22, 40, 0.88);
      --accent: #b77bff;
      --accent-2: #5ee1ff;
      --text: #e8ecff;
      --muted: #9aa3c7;
      --glow: 0 0 16px rgba(183, 123, 255, 0.35);
    }}

    * {{ box-sizing: border-box; }}

    body {{
      margin: 0;
      font-family: ""Segoe UI"", system-ui, sans-serif;
      background: radial-gradient(circle at top, #1a1f3d 0%, var(--bg) 50%), linear-gradient(145deg, #0b0f1f, #13172b);
      color: var(--text);
      min-height: 100vh;
      position: relative;
      overflow-x: hidden;
    }}

    body::before {{
      content: """";
      position: absolute;
      inset: 0;
      background-image: radial-gradient(#fff3 1px, transparent 1px);
      background-size: 120px 120px;
      opacity: 0.2;
      pointer-events: none;
    }}

    header {{
      position: sticky;
      top: 0;
      background: rgba(10, 12, 24, 0.85);
      backdrop-filter: blur(12px);
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
      padding: 24px;
      z-index: 2;
    }}

    .hero {{
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 24px;
      flex-wrap: wrap;
    }}

    .title {{
      display: flex;
      flex-direction: column;
      gap: 6px;
    }}

    .title h1 {{
      margin: 0;
      font-size: 28px;
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }}

    .title span {{
      color: var(--muted);
      font-size: 14px;
    }}

    .badge {{
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 8px 14px;
      border-radius: 999px;
      background: linear-gradient(120deg, rgba(183, 123, 255, 0.2), rgba(94, 225, 255, 0.2));
      border: 1px solid rgba(183, 123, 255, 0.35);
      font-size: 12px;
      color: var(--text);
      box-shadow: var(--glow);
    }}

    main {{
      padding: 32px 24px 64px;
      display: grid;
      gap: 24px;
      max-width: 1200px;
      margin: 0 auto;
      position: relative;
      z-index: 1;
    }}

    .panel {{
      background: var(--panel);
      border-radius: 20px;
      padding: 24px;
      border: 1px solid rgba(255, 255, 255, 0.08);
      box-shadow: 0 20px 40px rgba(0, 0, 0, 0.25);
      display: grid;
      gap: 16px;
    }}

    .panel h2 {{
      margin: 0;
      font-size: 20px;
    }}

    form {{
      display: grid;
      gap: 10px;
    }}

    label {{
      display: grid;
      gap: 6px;
      font-size: 14px;
      color: var(--muted);
    }}

    input,
    select,
    button {{
      width: 100%;
      padding: 10px;
      border-radius: 10px;
      border: 1px solid rgba(183, 123, 255, 0.35);
      background: rgba(19, 23, 43, 0.95);
      color: var(--text);
      transition: border-color 0.2s ease, box-shadow 0.2s ease, transform 0.2s ease;
    }}

    input:focus,
    select:focus {{
      outline: none;
      border-color: rgba(94, 225, 255, 0.6);
      box-shadow: 0 0 0 2px rgba(94, 225, 255, 0.15);
    }}

    button {{
      cursor: pointer;
      font-weight: 600;
      background: linear-gradient(135deg, rgba(183, 123, 255, 0.35), rgba(94, 225, 255, 0.35));
    }}

    button:hover {{
      transform: translateY(-1px);
      box-shadow: var(--glow);
    }}

    .result {{
      color: var(--muted);
      white-space: pre-wrap;
      font-size: 14px;
    }}

    .meta {{
      color: var(--muted);
      font-size: 13px;
    }}
  </style>
</head>
<body>
  <header>
    <div class=""hero"">
      <div class=""title"">
        <h1>AST Room Portal</h1>
        <span>🌌 Sphere Tracker · Commandes du thread</span>
      </div>
      <div class=""badge"">Mode: Thread</div>
    </div>
    <div id=""channel-meta"" class=""meta"">Channel ID: —</div>
  </header>

  <main>
    <section class=""panel"">
      <h2>Info</h2>
      <form data-command=""info"">
        <button type=""submit"">Afficher les infos du thread</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>Status games list</h2>
      <form data-command=""status-games-list"">
        <button type=""submit"">Afficher le statut des jeux</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>Get patch</h2>
      <form id=""patch-form"">
        <label>Alias
          <select id=""patch-alias-select"" name=""alias"" required>
            <option value="""">Chargement des alias…</option>
          </select>
        </label>
        <div class=""result"" id=""patch-link-result""></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>Update frequency check</h2>
      <form data-command=""update-frequency-check"">
        <label>Fréquence
          <select name=""checkFrequency"" required>
            <option value=""5m"">Toutes les 5 minutes</option>
            <option value=""15m"">Toutes les 15 minutes</option>
            <option value=""30m"">Toutes les 30 minutes</option>
            <option value=""1h"">Toutes les 1 heure</option>
            <option value=""6h"">Toutes les 6 heures</option>
            <option value=""12h"">Toutes les 12 heures</option>
            <option value=""18h"">Toutes les 18 heures</option>
            <option value=""1d"">Tous les jours</option>
          </select>
        </label>
        <button type=""submit"">Mettre à jour la fréquence</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>Update silent option</h2>
      <form data-command=""update-silent-option"">
        <label>Mode silencieux
          <select name=""silent"" required>
            <option value=""true"">Activé</option>
            <option value=""false"">Désactivé</option>
          </select>
        </label>
        <button type=""submit"">Mettre à jour le mode silencieux</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>Delete URL</h2>
      <form data-command=""delete-url"">
        <button type=""submit"">Supprimer l'URL du thread</button>
        <div class=""result"" data-result></div>
      </form>
    </section>
  </main>

<script>
  const params = new URLSearchParams(window.location.search);
  const m = window.location.pathname.match(/\/portal\/(\d+)\/(\d+)\/thread-commands\.html$/);

  const guildId = params.get('guildId') || (m ? m[1] : '');
  const channelId = params.get('channelId') || (m ? m[2] : '');

  document.getElementById('channel-meta').textContent = channelId ? ('Channel ID: ' + channelId) : 'Channel ID: —';

  const path = window.location.pathname;
  const idx = path.indexOf('/portal/');
  const basePath = idx >= 0 ? path.substring(0, idx) : '';

  const apiBase = window.location.origin + basePath + '/api/portal/' + guildId + '/' + channelId + '/thread-commands/execute';
  const patchAliasesApi = window.location.origin + basePath + '/api/portal/' + guildId + '/' + channelId + '/thread-commands/patches';

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
      patchLinkResult.textContent = 'Sélectionnez un alias pour voir le patch.';
      return;
    }}

    const entry = patchAliasData.get(alias);
    if (!entry) {{
      patchLinkResult.textContent = 'Alias introuvable.';
      return;
    }}

    const validPatchUrl = getValidPatchUrl(entry.patch);
    const gameLabel = entry.gameName ? ('Jeu: ' + entry.gameName) : 'Jeu: inconnu';

    if (validPatchUrl) {{
      patchLinkResult.innerHTML = gameLabel + '<br><a href=""' + escapeHtml(validPatchUrl) + '"" target=""_blank"" rel=""noopener noreferrer"">' + escapeHtml(validPatchUrl) + '</a>';
      return;
    }}

    patchLinkResult.textContent = gameLabel + '\nAucun lien de patch disponible pour cet alias.';
 }};

  const loadPatchAliases = async () => {{
    if (!patchAliasSelect || !patchLinkResult) return;

    if (!guildId || !channelId) {{
      patchAliasSelect.innerHTML = '<option value="""">Alias indisponibles</option>';
      patchAliasSelect.disabled = true;
      patchLinkResult.textContent = 'URL invalide: guildId/channelId introuvables.';
      return;
    }}

    patchAliasSelect.disabled = true;
    patchAliasSelect.innerHTML = '<option value="""">Chargement des alias…</option>';

    try {{
      const response = await fetch(patchAliasesApi);
      const payload = await parsePayload(response);

      if (!response.ok) {{
        const msg = extractMessage(payload, 'Erreur lors du chargement des alias.');
        patchAliasSelect.innerHTML = '<option value="""">Alias indisponibles</option>';
        patchLinkResult.textContent = msg;
        return;
      }}

      const aliases = payload && Array.isArray(payload.aliases) ? payload.aliases : [];
      patchAliasData.clear();

      if (aliases.length === 0) {{
        patchAliasSelect.innerHTML = '<option value="""">Aucun alias disponible</option>';
        patchLinkResult.textContent = 'Aucun alias trouvé pour ce thread.';
        return;
      }}

      const options = ['<option value="""">Sélectionnez un alias</option>'];
      aliases.forEach((entry) => {{
        if (!entry || !entry.alias) return;
        patchAliasData.set(entry.alias, {{ gameName: entry.gameName || '', patch: entry.patch || '' }});
        options.push('<option value=""' + escapeHtml(entry.alias) + '"">' + escapeHtml(entry.alias) + '</option>');
      }});

      patchAliasSelect.innerHTML = options.join('');
      patchAliasSelect.disabled = false;
      patchLinkResult.textContent = 'Sélectionnez un alias pour voir le patch.';
    }} catch {{
      patchAliasSelect.innerHTML = '<option value="""">Alias indisponibles</option>';
      patchLinkResult.textContent = 'Impossible de charger les alias.';
    }}
 }};

  if (patchAliasSelect) {{
    patchAliasSelect.addEventListener('change', () => renderPatchForAlias(patchAliasSelect.value));
 }}

  loadPatchAliases();

  document.querySelectorAll('form[data-command]').forEach((form) => {{
    form.addEventListener('submit', async (event) => {{
      event.preventDefault();
      const result = form.querySelector('[data-result]');

      if (!guildId || !channelId) {{
        showResult(result, 'URL invalide: guildId/channelId introuvables.', form.dataset.command);
        return;
     }}

      const data = new FormData(form);
      data.set('command', form.dataset.command);

      showResult(result, 'Traitement en cours...', form.dataset.command);

      try {{
        const response = await fetch(apiBase, {{method: 'POST', body: data}});
        const payload = await parsePayload(response);
        const msg = extractMessage(payload, response.ok ? 'Commande exécutée.' : 'Erreur lors de la commande.');
        showResult(result, msg, form.dataset.command);
     }} catch {{
        showResult(result, 'Impossible de joindre le serveur.', form.dataset.command);
     }}
   }});
 }});
</script>
</body>
</html>";
    }
}
