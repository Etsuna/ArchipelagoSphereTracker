using System.Net;
using System.Text;

public static class WebPortalPages
{
    private static string GetUserPortalUrl(string guildId, string channelId, string token)
    {
        var baseUrl = GetPortalBaseUrl();
        return $"{baseUrl}/portal/{guildId}/{channelId}/{token}/";
    }

    public static async Task<string?> EnsureUserPageAsync(string guildId, string channelId, string userId)
    {
        if (!Declare.EnableWebPortal)
        {
            return null;
        }

        var token = await PortalAccessCommands.EnsurePortalTokenAsync(guildId, channelId, userId);
        var userFolder = GetUserFolder(guildId, channelId, token);

        Directory.CreateDirectory(userFolder);

        var htmlPath = Path.Combine(userFolder, "index.html");
        var html = BuildHtmlPage(guildId, channelId, token);
        await File.WriteAllTextAsync(htmlPath, html, Encoding.UTF8);

        return GetUserPortalUrl(guildId, channelId, token);
    }

    public static async Task<string?> EnsureCommandsPageAsync(string guildId, string channelId)
    {
        if (!Declare.EnableWebPortal)
        {
            return null;
        }

        var channelFolder = Path.Combine(Declare.WebPortalPath, guildId, channelId);
        Directory.CreateDirectory(channelFolder);

        var htmlPath = Path.Combine(channelFolder, "commands.html");
        var html = BuildCommandsPage(channelId);
        await File.WriteAllTextAsync(htmlPath, html, Encoding.UTF8);

        return GetCommandsPortalUrl(guildId, channelId);
    }

    public static async Task EnsureMissingUserPagesAsync()
    {
        if (!Declare.EnableWebPortal)
        {
            return;
        }

        var users = await RecapListCommands.GetPortalUsersAsync();
        foreach (var (guildId, channelId, userId) in users)
        {
            await EnsureUserPageIfMissingAsync(guildId, channelId, userId);
        }
    }

    private static async Task EnsureUserPageIfMissingAsync(string guildId, string channelId, string userId)
    {
        var token = await PortalAccessCommands.EnsurePortalTokenAsync(guildId, channelId, userId);
        var userFolder = GetUserFolder(guildId, channelId, token);
        Directory.CreateDirectory(userFolder);

        var htmlPath = Path.Combine(userFolder, "index.html");
        if (File.Exists(htmlPath))
        {
            return;
        }

        var html = BuildHtmlPage(guildId, channelId, token);
        await File.WriteAllTextAsync(htmlPath, html, Encoding.UTF8);
    }

    public static void DeleteChannelPages(string guildId, string channelId)
    {
        if (!Declare.EnableWebPortal)
        {
            return;
        }

        var channelFolder = Path.Combine(Declare.WebPortalPath, guildId, channelId);
        if (Directory.Exists(channelFolder))
        {
            Directory.Delete(channelFolder, true);
        }

        var guildFolder = Path.Combine(Declare.WebPortalPath, guildId);
        if (Directory.Exists(guildFolder)
            && !Directory.EnumerateFileSystemEntries(guildFolder).Any())
        {
            Directory.Delete(guildFolder, true);
        }
    }

    public static void DeleteGuildPages(string guildId)
    {
        if (!Declare.EnableWebPortal)
        {
            return;
        }

        var guildFolder = Path.Combine(Declare.WebPortalPath, guildId);
        if (Directory.Exists(guildFolder))
        {
            Directory.Delete(guildFolder, true);
        }
    }

    private static string GetPortalBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(Declare.WebPortalBaseUrl))
        {
            return Declare.WebPortalBaseUrl.TrimEnd('/');
        }

        return $"http://localhost:{Declare.WebPortalPort}".TrimEnd('/');
    }

    private static string GetCommandsPortalUrl(string guildId, string channelId)
    {
        var baseUrl = GetPortalBaseUrl();
        return $"{baseUrl}/portal/{guildId}/{channelId}/commands.html";
    }

    private static string GetUserFolder(string guildId, string channelId, string token)
    {
        return Path.Combine(Declare.WebPortalPath, guildId, channelId, token);
    }

    private static string BuildHtmlPage(string guildId, string channelId, string token)
    {
        var safeGuildId = WebUtility.HtmlEncode(guildId);
        var safeChannelId = WebUtility.HtmlEncode(channelId);
        var safeToken = WebUtility.HtmlEncode(token);

        return $@"<!doctype html>
<html lang=""fr"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>AST Recap Portal</title>
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
      --danger: #ff6b7a;
      --glow: 0 0 16px rgba(183, 123, 255, 0.35);
    }}

    * {{
      box-sizing: border-box;
    }}

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

    details.panel {{
      padding: 0;
      overflow: hidden;
    }}

    details.panel[open] {{
      box-shadow: 0 22px 42px rgba(0, 0, 0, 0.28);
    }}

    details.panel > summary {{
      list-style: none;
      cursor: pointer;
      padding: 24px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      user-select: none;
    }}

    details.panel > summary::-webkit-details-marker {{
      display: none;
    }}

    details.panel > summary::after {{
      content: ""▾"";
      font-size: 18px;
      color: var(--accent-2);
      transition: transform 0.2s ease;
    }}

    details.panel[open] > summary::after {{
      transform: rotate(180deg);
    }}

    .panel-content {{
      padding: 0 24px 24px;
      display: grid;
      gap: 16px;
    }}

    .panel {{
      background: var(--panel);
      border-radius: 20px;
      padding: 24px;
      border: 1px solid rgba(255, 255, 255, 0.08);
      box-shadow: 0 20px 40px rgba(0, 0, 0, 0.25);
    }}

    .panel h2 {{
      margin-top: 0;
      display: flex;
      align-items: center;
      gap: 12px;
      font-size: 20px;
    }}

    .grid {{
      display: grid;
      gap: 16px;
    }}

    .alias-card {{
      border: 1px solid rgba(183, 123, 255, 0.3);
      border-radius: 16px;
      padding: 16px;
      background: rgba(15, 18, 33, 0.8);
    }}

    .alias-header {{
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 12px;
    }}

    .alias-header h3 {{
      margin: 0;
      font-size: 18px;
      color: var(--accent);
    }}

    .button {{
      background: linear-gradient(135deg, rgba(183, 123, 255, 0.35), rgba(94, 225, 255, 0.35));
      border: 1px solid rgba(183, 123, 255, 0.45);
      border-radius: 10px;
      color: var(--text);
      padding: 6px 14px;
      font-size: 12px;
      cursor: pointer;
      transition: transform 0.2s ease, box-shadow 0.2s ease;
    }}

    .button:hover {{
      transform: translateY(-1px);
      box-shadow: var(--glow);
    }}

    .button.danger {{
      background: rgba(255, 107, 122, 0.15);
      border-color: rgba(255, 107, 122, 0.5);
      color: #ffd7dc;
    }}

    .list {{
      list-style: none;
      padding: 0;
      margin: 0;
      display: grid;
      gap: 8px;
    }}

    .list li {{
      padding: 10px 12px;
      border-radius: 10px;
      background: rgba(255, 255, 255, 0.04);
      display: flex;
      flex-wrap: wrap;
      gap: 8px 16px;
      align-items: center;
    }}

    .group-title {{
      margin: 12px 0 6px;
      font-size: 12px;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      color: var(--muted);
    }}

    .tag {{
      padding: 2px 8px;
      border-radius: 999px;
      font-size: 11px;
      border: 1px solid rgba(94, 225, 255, 0.4);
      color: var(--accent-2);
    }}

    .meta {{
      color: var(--muted);
      font-size: 12px;
    }}

    .status {{
      margin-top: 8px;
      color: var(--accent-2);
      font-size: 13px;
    }}

    .empty {{
      color: var(--muted);
      font-style: italic;
    }}
  </style>
</head>
<body data-guild=""{safeGuildId}"" data-channel=""{safeChannelId}"" data-token=""{safeToken}"">
  <header>
    <div class=""hero"">
      <div class=""title"">
        <h1>AST Recap Portal</h1>
        <span>🌌 Sphere Tracker · Portail personnel</span>
      </div>
      <div class=""badge"">Accès: {safeToken}</div>
    </div>
    <div class=""meta"">Guild: {safeGuildId} · Channel: {safeChannelId}</div>
  </header>

  <main>
    <section class=""panel"">
      <h2>🔭 Actions rapides</h2>
      <button class=""button"" id=""refresh"">Rafraîchir les données</button>
      <div id=""status"" class=""status""></div>
    </section>

    <details class=""panel"" open>
      <summary>
        <h2>📜 Recap en cours</h2>
      </summary>
      <div id=""recap-root"" class=""grid panel-content""></div>
    </details>

    <details class=""panel"" open>
      <summary>
        <h2>✨ Hints actifs</h2>
      </summary>
      <div id=""hints-root"" class=""grid panel-content""></div>
    </details>

    <details class=""panel"" open>
      <summary>
        <h2>🎁 Items reçus</h2>
      </summary>
      <div id=""items-root"" class=""grid panel-content""></div>
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
    const itemsRoot = document.getElementById('items-root');
    const hintsRoot = document.getElementById('hints-root');

    const path = window.location.pathname; // ex: /AST/portal/g/c/token/
    const i = path.indexOf('/portal/');
    const prefix = i >= 0 ? path.substring(0, i) : ''; // ex: /AST ou '' en direct
    const apiBase = `${{window.location.origin}}${{prefix}}/api/portal/${{ctx.guildId}}/${{ctx.channelId}}/${{ctx.token}}`;

    const escapeHtml = (value) => {{
      const div = document.createElement('div');
      div.textContent = value ?? '';
      return div.innerHTML;
    }};

    const setStatus = (message) => {{
      status.textContent = message;
    }};

    const createAliasCard = (alias, content, actions) => {{
      const card = document.createElement('div');
      card.className = 'alias-card';

      const header = document.createElement('div');
      header.className = 'alias-header';

      const title = document.createElement('h3');
      title.textContent = alias;
      header.appendChild(title);

      if (actions) {{
        header.appendChild(actions);
      }}

      card.appendChild(header);
      card.appendChild(content);
      return card;
    }};

    const renderRecaps = (recaps) => {{
      recapRoot.innerHTML = '';
      if (!recaps || recaps.length === 0) {{
        recapRoot.innerHTML = '<p class=""empty"">Aucun recap actif pour cet utilisateur.</p>';
        return;
      }}

      recaps.forEach(recap => {{
        const container = document.createElement('div');

        if (!recap.groups || recap.groups.length === 0) {{
          const list = document.createElement('ul');
          list.className = 'list';

          const item = document.createElement('li');
          item.textContent = 'Aucun item en attente.';
          list.appendChild(item);

          container.appendChild(list);
        }} else {{
          recap.groups.forEach(group => {{
            const title = document.createElement('div');
            title.className = 'group-title';
            title.textContent = `Flag: ${{group.flagLabel}}`;
            container.appendChild(title);

            const list = document.createElement('ul');
            list.className = 'list';

            if (group.items.length === 0) {{
              const item = document.createElement('li');
              item.textContent = 'Aucun item en attente.';
              list.appendChild(item);
            }} else {{
              group.items.forEach(it => {{
                const item = document.createElement('li');
                const suffix = it.count > 1 ? ` ×${{it.count}}` : '';
                item.innerHTML = `<strong>${{escapeHtml(it.item)}}</strong>${{suffix}}`;

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
        actions.textContent = 'Supprimer le recap';
        actions.addEventListener('click', () => deleteRecap(recap.alias));

        recapRoot.appendChild(createAliasCard(recap.alias, container, actions));
      }});
    }};

    const renderItems = (items) => {{
      itemsRoot.innerHTML = '';
      if (!items || items.length === 0) {{
        itemsRoot.innerHTML = '<p class=""empty"">Aucun item reçu pour le moment.</p>';
        return;
      }}

      items.forEach(group => {{
        const container = document.createElement('div');

        if (!group.groups || group.groups.length === 0) {{
          const list = document.createElement('ul');
          list.className = 'list';

          const entry = document.createElement('li');
          entry.textContent = 'Aucun item reçu.';
          list.appendChild(entry);

          container.appendChild(list);
        }} else {{
          group.groups.forEach(flagGroup => {{
            const title = document.createElement('div');
            title.className = 'group-title';
            title.textContent = `Flag: ${{flagGroup.flagLabel}}`;
            container.appendChild(title);

            const list = document.createElement('ul');
            list.className = 'list';

            if (flagGroup.items.length === 0) {{
              const entry = document.createElement('li');
              entry.textContent = 'Aucun item reçu.';
              list.appendChild(entry);
            }} else {{
              flagGroup.items.forEach(it => {{
                const entry = document.createElement('li');
                entry.innerHTML = `<strong>${{escapeHtml(it.item)}}</strong> <span class=""meta"">(${{escapeHtml(it.game)}})</span>`;

                const detail = document.createElement('div');
                detail.className = 'meta';
                detail.textContent = `Finder: ${{it.finder}} · Location: ${{it.location}}`;
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

    const renderHints = (hints) => {{
      hintsRoot.innerHTML = '';
      if (!hints || hints.length === 0) {{
        hintsRoot.innerHTML = '<p class=""empty"">Aucun hint actif.</p>';
        return;
      }}

      hints.forEach(group => {{
        const wrapper = document.createElement('div');
        wrapper.className = 'grid';

        const receiverList = document.createElement('ul');
        receiverList.className = 'list';
        if (group.asReceiver.length === 0) {{
          const entry = document.createElement('li');
          entry.textContent = 'Aucun hint en tant que Receiver.';
          receiverList.appendChild(entry);
        }} else {{
          group.asReceiver.forEach(hint => {{
            const entry = document.createElement('li');
            entry.innerHTML = `<strong>${{escapeHtml(hint.item)}}</strong> <span class=""meta"">@${{escapeHtml(hint.location)}}</span>`;
            entry.appendChild(makeHintMeta(hint, true));
            receiverList.appendChild(entry);
          }});
        }}

        const finderList = document.createElement('ul');
        finderList.className = 'list';
        if (group.asFinder.length === 0) {{
          const entry = document.createElement('li');
          entry.textContent = 'Aucun hint en tant que Finder.';
          finderList.appendChild(entry);
        }} else {{
          group.asFinder.forEach(hint => {{
            const entry = document.createElement('li');
            entry.innerHTML = `<strong>${{escapeHtml(hint.item)}}</strong> <span class=""meta"">@${{escapeHtml(hint.location)}}</span>`;
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

    const makeHintMeta = (hint, isReceiver) => {{
      const meta = document.createElement('div');
      meta.className = 'meta';
      meta.textContent = isReceiver
        ? `Finder: ${{hint.finder}} · Game: ${{hint.game}}`
        : `Receiver: ${{hint.receiver}} · Game: ${{hint.game}}`;
      return meta;
    }};

    const deleteRecap = async (alias) => {{
      setStatus('Suppression du recap...');
      const formData = new FormData();
      formData.append('alias', alias);

      const res = await fetch(`${{apiBase}}/recap/delete`, {{
        method: 'POST',
        body: formData
      }});

      if (res.ok) {{
        setStatus(`Recap supprimé pour ${{alias}}.`);
        await loadData();
      }} else {{
        setStatus('Impossible de supprimer le recap.');
      }}
    }};

    const loadData = async () => {{
      setStatus('Synchronisation avec la base de données...');
      const res = await fetch(`${{apiBase}}/summary`);
      if (!res.ok) {{
        setStatus('Portail indisponible.');
        return;
      }}

      const data = await res.json();
      renderRecaps(data.recaps || []);
      renderItems(data.receivedItems || []);
      renderHints(data.hints || []);
      setStatus(`Dernière mise à jour: ${{new Date(data.lastUpdated).toLocaleString()}}`);
    }};

    document.getElementById('refresh').addEventListener('click', loadData);
    loadData();
  </script>
</body>
</html>";
    }

    private static string BuildCommandsPage(string channelId)
    {
        var modeLabel = Declare.IsArchipelagoMode ? "Archipelago" : "Standard";
        var archipelagoSections = Declare.IsArchipelagoMode
            ? $@"
    <section class=""panel"">
      <h2>YAML</h2>
      <form data-command=""list-yamls"">
        <button type=""submit"">Lister les YAML</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""backup-yamls"">
        <button type=""submit"">Backup YAML (ZIP)</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""delete-yaml"">
        <label>Nom du fichier YAML
          <input name=""fileName"" placeholder=""example.yaml"" required />
        </label>
        <button type=""submit"">Supprimer le YAML</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""clean-yamls"">
        <button type=""submit"">Nettoyer tous les YAML</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""send-yaml"">
        <label>Uploader un YAML
          <input type=""file"" name=""file"" accept="".yaml"" required />
        </label>
        <button type=""submit"">Envoyer le YAML</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""download-template"">
        <label>Template YAML
          <input name=""template"" placeholder=""template.yaml"" required />
        </label>
        <button type=""submit"">Télécharger le template</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>APWorld</h2>
      <form data-command=""list-apworld"">
        <button type=""submit"">Lister les APWorld</button>
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
        <button type=""submit"">Envoyer l'APWorld</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>Génération</h2>
      <form data-command=""generate"">
        <button type=""submit"">Générer</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""test-generate"">
        <button type=""submit"">Test génération</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""generate-with-zip"">
        <label>ZIP de YAML
          <input type=""file"" name=""file"" accept="".zip"" required />
        </label>
        <button type=""submit"">Générer avec ZIP</button>
        <div class=""result"" data-result></div>
      </form>
    </section>"
            : string.Empty;

        return $@"<!doctype html>
<html lang=""fr"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>Commandes Discord</title>
  <style>
    :root {{
      color-scheme: dark;
      --bg: #0b0f1f;
      --panel: rgba(18, 22, 40, 0.92);
      --accent: #b77bff;
      --text: #e8ecff;
      --muted: #9aa3c7;
    }}

    * {{
      box-sizing: border-box;
    }}

    body {{
      margin: 0;
      font-family: ""Segoe UI"", system-ui, sans-serif;
      background: radial-gradient(circle at top, #1a1f3d 0%, var(--bg) 55%);
      color: var(--text);
      min-height: 100vh;
      padding: 32px 20px 48px;
    }}

    main {{
      max-width: 1000px;
      margin: 0 auto;
      background: var(--panel);
      padding: 28px;
      border-radius: 20px;
      box-shadow: 0 20px 40px rgba(0, 0, 0, 0.35);
    }}

    h1 {{
      margin-top: 0;
      font-size: 26px;
      letter-spacing: 0.05em;
      text-transform: uppercase;
    }}

    .mode {{
      color: var(--muted);
      margin-top: -8px;
      margin-bottom: 24px;
    }}

    .meta {{
      color: var(--muted);
      font-size: 14px;
    }}

    .panel {{
      border: 1px solid rgba(183, 123, 255, 0.2);
      border-radius: 16px;
      background: rgba(13, 16, 32, 0.7);
      padding: 18px;
      margin-bottom: 18px;
      display: grid;
      gap: 12px;
    }}

    form {{
      display: grid;
      gap: 10px;
      padding: 12px;
      border-radius: 12px;
      background: rgba(9, 11, 24, 0.6);
    }}

    label {{
      display: grid;
      gap: 6px;
      font-size: 14px;
      color: var(--muted);
    }}

    input, select, button {{
      padding: 10px 12px;
      border-radius: 10px;
      border: 1px solid rgba(255, 255, 255, 0.08);
      background: rgba(6, 8, 20, 0.8);
      color: var(--text);
      font-size: 14px;
    }}

    button {{
      cursor: pointer;
      background: linear-gradient(135deg, rgba(183, 123, 255, 0.35), rgba(94, 225, 255, 0.35));
      border-color: rgba(183, 123, 255, 0.5);
      font-weight: 600;
    }}

    .result {{
      font-size: 13px;
      color: var(--muted);
    }}

    .result a {{
      color: var(--accent);
      text-decoration: none;
    }}

    .layout {{
      display: grid;
      gap: 18px;
    }}
  </style>
</head>
<body>
  <main>
    <h1>Commandes disponibles</h1>
    <p class=""mode"">Mode: {WebUtility.HtmlEncode(modeLabel)}</p>

    <section class=""panel"">
      <h2>Configuration</h2>
      <div class=""meta"">Channel ID: {WebUtility.HtmlEncode(channelId)}</div>
      <label>User ID (optionnel, utile pour threads privés)
        <input id=""user-id"" placeholder=""123456789012345678"" />
      </label>
    </section>

    <section class=""panel"">
      <h2>Créer un thread via /add-url</h2>
      <form data-command=""add-url"">
        <input type=""hidden"" name=""userId"" />
        <label>URL Archipelago
          <input name=""url"" placeholder=""https://archipelago.gg/room/XXXX"" required />
        </label>
        <label>Nom du thread
          <input name=""threadName"" placeholder=""Archipelago"" />
        </label>
        <label>Type de thread
          <select name=""threadType"">
            <option value=""Private"">Privé</option>
            <option value=""Public"">Public</option>
          </select>
        </label>
        <label>Auto-ajouter les membres (public)
          <select name=""autoAddMembers"">
            <option value=""false"">Non</option>
            <option value=""true"">Oui</option>
          </select>
        </label>
        <label>Mode silencieux
          <select name=""silent"">
            <option value=""false"">Non</option>
            <option value=""true"">Oui</option>
          </select>
        </label>
        <label>Fréquence de check
          <select name=""checkFrequency"">
            <option value=""5m"">Toutes les 5 minutes</option>
            <option value=""15m"">Toutes les 15 minutes</option>
            <option value=""30m"">Toutes les 30 minutes</option>
            <option value=""1h"">Toutes les 1h</option>
            <option value=""6h"">Toutes les 6h</option>
            <option value=""12h"">Toutes les 12h</option>
            <option value=""18h"">Toutes les 18h</option>
            <option value=""1d"">Chaque jour</option>
          </select>
        </label>
        <button type=""submit"">Créer le thread</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    <section class=""panel"">
      <h2>Infos utiles</h2>
      <form data-command=""apworlds-info"">
        <button type=""submit"">APWorlds info</button>
        <div class=""result"" data-result></div>
      </form>
      <form data-command=""discord"">
        <button type=""submit"">Discord</button>
        <div class=""result"" data-result></div>
      </form>
    </section>

    {archipelagoSections}
  </main>
  <script>
    const path = window.location.pathname; // ex: /portal/guildId/channelId/commands.html
    const i = path.indexOf('/portal/');
    const prefix = i >= 0 ? path.substring(0, i) : '';
    const parts = path.split('/').filter(Boolean);
    const guildId = parts[1] || '';
    const channelId = parts[2] || '';
    const apiBase = `${{window.location.origin}}${{prefix}}/api/portal/${{guildId}}/${{channelId}}/commands/execute`;
    const userInput = document.getElementById('user-id');

    const showResult = (container, message, downloadUrl) => {{
      container.innerHTML = '';
      if (message) {{
        const msg = document.createElement('div');
        msg.textContent = message;
        container.appendChild(msg);
      }}
      if (downloadUrl) {{
        const link = document.createElement('a');
        link.href = downloadUrl;
        link.textContent = 'Télécharger le fichier';
        link.target = '_blank';
        container.appendChild(link);
      }}
    }};

    document.querySelectorAll('form[data-command]').forEach((form) => {{
      form.addEventListener('submit', async (event) => {{
        event.preventDefault();
        const result = form.querySelector('[data-result]');
        const data = new FormData(form);
        data.set('command', form.dataset.command);
        if (form.dataset.command === 'add-url' && userInput.value.trim()) {{
          data.set('userId', userInput.value.trim());
        }}

        showResult(result, 'Traitement en cours...', null);

        try {{
          const response = await fetch(apiBase, {{
            method: 'POST',
            body: data
          }});

          const payload = await response.json().catch(() => ({{ message: 'Réponse invalide.' }}));
          if (!response.ok) {{
            showResult(result, payload.message || 'Erreur lors de la commande.', null);
            return;
          }}

          showResult(result, payload.message || 'Commande exécutée.', payload.downloadUrl);
        }} catch (error) {{
          showResult(result, 'Impossible de joindre le serveur.', null);
        }}
      }});
    }});
  </script>
</body>
</html>";
    }
}
