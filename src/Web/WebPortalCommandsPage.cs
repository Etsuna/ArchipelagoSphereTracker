using System.Net;

public static class WebPortalCommandsPage
{
    public static string Build()
    {
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
            : @"<option value=\""disabled selected>Aucun template disponible</option>";

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
        <label>Fichier YAML à supprimer
          <select name=""fileName"" data-yaml-select required>
            <option value="""" selected disabled>Chargement des YAML...</option>
          </select>
        </label>
        <button type=""submit"">Supprimer le YAML</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""clean-yamls"">
        <button type=""submit"">Nettoyer tous les YAML</button>
        <div class=""result"" data-result></div>
      </form>

      <form data-command=""download-yaml"">
        <label>Fichier YAML à télécharger
          <select name=""fileName"" data-yaml-select required>
            <option value="""" selected disabled>Chargement des YAML...</option>
          </select>
        </label>
        <button type=""submit"">Télécharger le YAML</button>
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
          <select name=""template"" required>
            {templateSelectOptions}
          </select>
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
      gap: 12px;
    }}

    .panel h2 {{
      margin-top: 0;
      display: flex;
      align-items: center;
      gap: 12px;
      font-size: 20px;
    }}

    .mode {{
      color: var(--muted);
      margin-top: -4px;
      margin-bottom: 8px;
    }}

    .meta {{
      color: var(--muted);
      font-size: 14px;
    }}

    form {{
      display: grid;
      gap: 10px;
      padding: 14px;
      border-radius: 16px;
      background: rgba(9, 11, 24, 0.55);
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
      border: 1px solid rgba(183, 123, 255, 0.45);
      font-weight: 600;
      transition: transform 0.2s ease, box-shadow 0.2s ease;
    }}

    button:hover {{
      transform: translateY(-1px);
      box-shadow: var(--glow);
    }}

    .result {{
      font-size: 13px;
      color: var(--muted);
      white-space: pre-wrap;
    }}

    .result a {{
      color: var(--accent);
      text-decoration: none;
    }}
  </style>
</head>
<body>
  <header>
    <div class=""hero"">
      <div class=""title"">
        <h1>AST Portal</h1>
        <span>⚙️ Commandes Discord</span>
      </div>
      <div class=""badge"">Mode: {WebUtility.HtmlEncode(modeLabel)}</div>
    </div>
    <div class=""meta"" id=""channel-meta"">Channel ID: —</div>
  </header>

  <main>
    <section class=""panel"">
      <h2>Configuration</h2>
      <p class=""mode"">Renseignez un user ID si vous ciblez un thread privé.</p>
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
  const params = new URLSearchParams(window.location.search);

  // Attend /portal/{{guildId}}/{{channelId}}/commands.html (ids numériques)
  const m = window.location.pathname.match(/\/portal\/(\d+)\/(\d+)\/commands\.html$/);

  const guildId = params.get('guildId') || (m ? m[1] : '');
  const channelId = params.get('channelId') || (m ? m[2] : '');

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
    '/commands/execute';

  const yamlsApi =
    window.location.origin +
    basePath +
    '/api/portal/' +
    guildId +
    '/' +
    channelId +
    '/commands/yamls';

  const userInput = document.getElementById('user-id');
  const yamlSelects = document.querySelectorAll('[data-yaml-select]');

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
      link.textContent = 'Télécharger le fichier';
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

  const loadYamlOptions = async () => {{
    if (!yamlSelects.length || !guildId || !channelId) return;

    setYamlSelectOptions('<option value="" selected disabled>Chargement des YAML...</option>');

    try {{
      const response = await fetch(yamlsApi);
      const payload = await parsePayload(response);
      const files = payload && typeof payload === 'object' && Array.isArray(payload.files)
        ? payload.files
        : [];

      if (!response.ok || files.length === 0) {{
        setYamlSelectOptions('<option value="" selected disabled>Aucun YAML disponible</option>');
        return;
      }}

      yamlSelects.forEach((select) => {{
        select.innerHTML = '<option value="" selected disabled>Sélectionner un YAML</option>';
        files.forEach((fileName) => {{
          const option = document.createElement('option');
          option.value = fileName;
          option.textContent = fileName;
          select.appendChild(option);
        }});
      }});
    }} catch {{
      setYamlSelectOptions('<option value="" selected disabled>Erreur de chargement des YAML</option>');
    }}
  }};


  document.querySelectorAll('form[data-command]').forEach((form) => {{
    form.addEventListener('submit', async (event) => {{
      event.preventDefault();

      const result = form.querySelector('[data-result]');
      if (!guildId || !channelId) {{
        showResult(
          result,
          'URL invalide: guildId/channelId introuvables. Ouvre la page via /portal/{{guildId}}/{{channelId}}/commands.html',
          null
        );
        return;
      }}

      const data = new FormData(form);
      data.set('command', form.dataset.command);

      if (['delete-yaml', 'download-yaml'].includes(form.dataset.command)) {{
        const selectedYaml = data.get('fileName');
        if (!selectedYaml) {{
          showResult(result, 'Aucun YAML sélectionné.', null);
          return;
        }}
      }}

      // Ne pas envoyer userId vide
      if (form.dataset.command === 'add-url') {{
        const v = userInput.value.trim();
        if (v) data.set('userId', v);
        else data.delete('userId');
      }}

      showResult(result, 'Traitement en cours...', null);

      try {{
        const response = await fetch(apiBase, {{
          method: 'POST',
          body: data
        }});

        const payload = await parsePayload(response);
        const msg = extractMessage(
          payload,
          response.ok ? 'Commande exécutée.' : 'Erreur lors de la commande.'
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
        showResult(result, 'Impossible de joindre le serveur.', null);
      }}
    }});
  }});

  loadYamlOptions();
</script>
</body>
</html>";
    }
}
