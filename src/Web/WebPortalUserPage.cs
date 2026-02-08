using System.Net;

public static class WebPortalUserPage
{
    public static string Build(string guildId, string channelId, string token)
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

    details.panel > summary::-webkit-details-marker {{ display: none; }}

    details.panel > summary::after {{
      content: ""▾"";
      font-size: 18px;
      color: var(--accent-2);
      transition: transform 0.2s ease;
    }}

    details.panel[open] > summary::after {{ transform: rotate(180deg); }}

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

    .grid {{ display: grid; gap: 16px; }}

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

    .button:hover {{ transform: translateY(-1px); box-shadow: var(--glow); }}

    .button.danger {{
      background: rgba(255, 107, 122, 0.15);
      border-color: rgba(255, 107, 122, 0.5);
      color: #ffd7dc;
    }}

    .actions-grid {{
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 12px;
      margin-top: 12px;
    }}

    .action-group {{
      display: grid;
      gap: 8px;
    }}

    .action-group label {{
      font-size: 13px;
      color: var(--muted);
    }}

    .action-group select {{
      width: 100%;
      border-radius: 10px;
      padding: 8px 10px;
      border: 1px solid rgba(255, 255, 255, 0.12);
      background: rgba(11, 15, 31, 0.85);
      color: var(--text);
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

    .meta {{ color: var(--muted); font-size: 12px; }}

    .hero-info {{
      margin-top: 10px;
      color: var(--muted);
      font-size: 13px;
      line-height: 1.5;
      white-space: pre-wrap;
    }}

    .hero-info a {{
      color: var(--accent-2);
    }}

    .status {{ margin-top: 8px; color: var(--accent-2); font-size: 13px; }}

    .empty {{ color: var(--muted); font-style: italic; }}
  </style>
</head>
<body data-guild=""{safeGuildId}"" data-channel=""{safeChannelId}"" data-token=""{safeToken}"">
  <header>
    <div class=""hero"">
      <div class=""title"">
        <h1>AST User Portal</h1>
        <span>🌌 Sphere Tracker · Portail personnel</span>
      </div>
      <div class=""badge"">Accès: {safeToken}</div>
    </div>
    <div class=""meta"">Guild: {safeGuildId} · Channel: {safeChannelId}</div>
    <div class=""hero-info"" id=""hero-info"">Chargement des infos…</div>
  </header>

  <main>
    <section class=""panel"">
      <h2>🔭 Actions rapides</h2>
      <button class=""button"" id=""refresh"">Rafraîchir les données</button>
      <div id=""status"" class=""status""></div>
    </section>
   
    <details class=""panel"" open>
      <summary><h2>🧑‍🚀 Vos alias</h2></summary>
      <div class=""actions-grid panel-content"">
        <div class=""action-group"">
          <label for=""add-alias-select"">Ajouter un alias existant dans ce thread:</label>
          <select id=""add-alias-select""></select>
          <button class=""button"" id=""add-alias-button"">Ajouter l'alias sélectionné</button>
        </div>
        <div class=""action-group"">
          <label for=""delete-alias-select"">Supprimer un alias de votre liste:</label>
          <select id=""delete-alias-select""></select>
          <button class=""button danger"" id=""delete-alias-button"">Supprimer l'alias sélectionné</button>
        </div>
      </div>
    </details>

    <details class=""panel"" open>
      <summary><h2>📜 Recap en cours</h2></summary>
      <div id=""recap-root"" class=""grid panel-content""></div>
    </details>

    <details class=""panel"" open>
      <summary><h2>✨ Hints actifs</h2></summary>
      <div id=""hints-root"" class=""grid panel-content""></div>
    </details>

    <details class=""panel"" open>
      <summary><h2>🎁 Items reçus</h2></summary>
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
    const addAliasSelect = document.getElementById('add-alias-select');
    const deleteAliasSelect = document.getElementById('delete-alias-select');
    const addAliasButton = document.getElementById('add-alias-button');
    const deleteAliasButton = document.getElementById('delete-alias-button');
    const heroInfo = document.getElementById('hero-info');

    const path = window.location.pathname; // ex: /AST/portal/g/c/token/
    const idx = path.indexOf('/portal/');
    const basePath = idx >= 0 ? path.substring(0, idx) : '';
    const apiBase = window.location.origin + basePath + '/api/portal/' + ctx.guildId + '/' + ctx.channelId + '/' + ctx.token;
    const infoApi = window.location.origin + basePath + '/api/portal/' + ctx.guildId + '/' + ctx.channelId + '/info';

    const escapeHtml = (value) => {{
      const div = document.createElement('div');
      div.textContent = value ?? '';
      return div.innerHTML;
    }};

    const setStatus = (message) => {{
      status.textContent = message;
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
        const message = payload && payload.message ? payload.message : (response.ok ? '' : 'Impossible de charger les infos.');
        heroInfo.innerHTML = linkifyText(message || 'Aucune info disponible.');
      }} catch {{
        heroInfo.textContent = 'Impossible de charger les infos.';
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
            title.textContent = 'Flag: ' + group.flagLabel;
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
            title.textContent = 'Flag: ' + flagGroup.flagLabel;
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
          entry.textContent = 'Aucun hint en tant que Finder.';
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
      setStatus('Suppression du recap...');
      const formData = new FormData();
      formData.append('alias', alias);

      const res = await fetch(apiBase + '/recap/delete', {{
        method: 'POST',
        body: formData
      }});

      if (res.ok) {{
        setStatus('Recap supprimé pour ' + alias + '.');
        await loadData();
      }} else {{
        setStatus('Impossible de supprimer le recap.');
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

    const loadAliasLists = async () => {{
      addAliasSelect.innerHTML = '<option value="""">Chargement des alias du thread...</option>';
      deleteAliasSelect.innerHTML = '<option value="""">Chargement de vos alias...</option>';

      try {{
        const [allRes, userRes] = await Promise.all([
          fetch(apiBase + '/aliases'),
          fetch(apiBase + '/aliases/user')
        ]);

        if (allRes.ok) {{
          const payload = await allRes.json();
          fillSelect(addAliasSelect, payload.aliases || [], 'Aucun alias disponible dans ce thread', 'Sélectionnez un alias');
        }} else {{
          fillSelect(addAliasSelect, [], 'Impossible de charger les alias du thread', 'Sélectionnez un alias');
        }}

        if (userRes.ok) {{
          const payload = await userRes.json();
          fillSelect(deleteAliasSelect, payload.aliases || [], 'Vous n\'avez aucun alias enregistré', 'Sélectionnez un alias');
        }} else {{
          fillSelect(deleteAliasSelect, [], 'Impossible de charger vos alias', 'Sélectionnez un alias');
        }}
      }} catch (e) {{
        fillSelect(addAliasSelect, [], 'Impossible de charger les alias du thread', 'Sélectionnez un alias');
        fillSelect(deleteAliasSelect, [], 'Impossible de charger vos alias', 'Sélectionnez un alias');
      }}
    }};

    const addAliasFromPortal = async () => {{
      const alias = addAliasSelect.value;
      if (!alias) {{
        setStatus('Sélectionnez un alias à ajouter.');
        return;
      }}

      setStatus(""Ajout de l'alias..."");
      const formData = new FormData();
      formData.append('alias', alias);

      const res = await fetch(apiBase + '/alias/add', {{ method: 'POST', body: formData }});
      if (res.ok) {{
        setStatus('Alias ajouté: ' + alias + '.');
        await loadData();
        return;
      }}

      const payload = await res.json().catch(() => ({{}}));
      setStatus(payload.message || ""Impossible d'ajouter cet alias."");
    }};

    const deleteAliasFromPortal = async () => {{
      const alias = deleteAliasSelect.value;
      if (!alias) {{
        setStatus('Sélectionnez un alias à supprimer.');
        return;
      }}

      setStatus(""Suppression de l'alias..."");
      const formData = new FormData();
      formData.append('alias', alias);

      const res = await fetch(apiBase + '/alias/delete', {{ method: 'POST', body: formData }});
      if (res.ok) {{
        setStatus('Alias supprimé: ' + alias + '.');
        await loadData();
        return;
      }}

      const payload = await res.json().catch(() => ({{}}));
      setStatus(payload.message || 'Impossible de supprimer cet alias.');
    }};

    const loadData = async () => {{
      setStatus('Synchronisation avec la base de données...');
      const res = await fetch(apiBase + '/summary');
      if (!res.ok) {{
        await loadAliasLists();
        setStatus('Portail indisponible.');
        return;
      }}

      const data = await res.json();
      renderRecaps(data.recaps || []);
      renderItems(data.receivedItems || []);
      renderHints(data.hints || []);
      await loadAliasLists();
      setStatus('Dernière mise à jour: ' + new Date(data.lastUpdated).toLocaleString());
    }};

    document.getElementById('refresh').addEventListener('click', loadData);
    addAliasButton.addEventListener('click', addAliasFromPortal);
    deleteAliasButton.addEventListener('click', deleteAliasFromPortal);
    loadHeroInfo();
    loadData();
  </script>
</body>
</html>";
    }
}
