public static class WebPortalTheme
{
    public const string Styles = """
    :root { color-scheme: dark; --bg:#10151d; --bg-2:#171e28; --panel:#171e28; --text:#e4eaf2; --muted:#9aa9bd; --accent:#ac99ed; --accent-2:#87bddf; --danger:#ee8e97; --line:#2b3544; --glow:none; }
    * { box-sizing:border-box; }
    [hidden] { display:none !important; }
    .sr-only { position:absolute; width:1px; height:1px; padding:0; margin:-1px; overflow:hidden; clip:rect(0,0,0,0); white-space:nowrap; border:0; }
    html { scroll-behavior:smooth; }
    body { margin:0; background:var(--bg); color:var(--text); font:14px/1.5 "Segoe UI",system-ui,sans-serif; display:grid; grid-template-columns:210px minmax(0,1fr); min-height:100vh; align-content:start; }
    body::before { display:none; }
    header { grid-column:1/-1; padding:20px 28px 14px; background:#141b25; border-bottom:1px solid var(--line); position:static; }
    .hero { display:flex; align-items:center; justify-content:space-between; flex-wrap:wrap; gap:12px; }
    .title h1 { margin:0; font-size:23px; font-weight:650; letter-spacing:-.4px; }
    .title span,.meta,.mode,.companion-help { color:var(--muted); font-size:12px; }
    .badge,.tag { display:inline-flex; align-items:center; gap:6px; padding:3px 9px; border:1px solid var(--line); border-radius:5px; color:var(--muted); font-size:12px; background:#1c2532; }
    .portal-nav { grid-column:1; padding:22px 16px; display:flex; flex-direction:column; gap:4px; align-self:start; position:sticky; top:0; max-height:100vh; overflow:auto; }
    .portal-nav a { color:var(--muted); text-decoration:none; padding:8px 10px; border-radius:5px; font-size:13px; border-left:2px solid transparent; }
    .portal-nav a:hover,.portal-nav a[aria-current] { color:var(--text); background:#202938; border-left-color:var(--accent); }
    main { grid-column:2; width:100%; min-width:0; max-width:1600px; margin:0 auto; padding:22px 28px 52px 12px; display:grid; gap:18px; align-content:start; }
    .panel { background:var(--panel); border:1px solid var(--line); border-radius:7px; padding:18px; min-width:0; scroll-margin-top:16px; box-shadow:none; }
    .panel h2 { margin:0 0 12px; font-size:16px; font-weight:600; letter-spacing:0; }
    .panel h3 { font-size:14px; margin:0 0 10px; }
    details.panel { padding:0; }
    details.panel > summary { display:flex; justify-content:space-between; align-items:center; padding:14px 18px; cursor:pointer; list-style:none; gap:12px; }
    details.panel > summary::-webkit-details-marker { display:none; }
    details.panel > summary::after { content:"+"; color:var(--muted); font-size:18px; }
    details.panel[open] > summary::after { content:"−"; }
    details.panel > summary h2 { margin:0; }
    .panel-content { padding:0 18px 18px; }
    .hero-details { margin-top:10px; color:var(--muted); font-size:12px; }
    .hero-details summary { cursor:pointer; }
    .hero-info { white-space:pre-wrap; padding:8px 0; overflow-wrap:anywhere; }
    a { color:var(--accent-2); }
    form,.action-group { display:grid; gap:9px; align-content:start; }
    form + form { border-top:1px solid var(--line); padding-top:14px; margin-top:14px; }
    label { display:grid; gap:5px; font-size:12px; color:var(--muted); }
    input,select,textarea { width:100%; min-width:0; border:1px solid #3b4759; border-radius:5px; padding:8px 10px; background:#111822; color:var(--text); font:inherit; }
    input[type=checkbox] { width:16px; height:16px; accent-color:var(--accent); }
    button,.button { font:inherit; font-size:12px; font-weight:600; cursor:pointer; border:1px solid #4a4564; border-radius:5px; background:#302b43; color:#ece6ff; padding:7px 12px; width:auto; justify-self:start; transition:background .15s; }
    button:hover,.button:hover { background:#403655; transform:none; box-shadow:none; }
    button:disabled,.button:disabled { opacity:.45; cursor:not-allowed; }
    button.danger,.button.danger { border-color:#704149; color:#f2b1b8; background:#38262d; }
    :focus-visible { outline:2px solid var(--accent-2); outline-offset:3px; }
    .actions-grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(220px,1fr)); gap:16px; }
    .companion-group,.spoiler-status { grid-column:1/-1; }
    .companion-group { border-top:1px solid var(--line); padding-top:14px; }
    .companion-actions { display:flex; flex-wrap:wrap; gap:8px; }
    .compact-upload { align-self:start; }
    .compact-upload .button { justify-self:start; }
    .grid { display:grid; gap:10px; }
    .alias-card { border:1px solid var(--line); border-radius:5px; padding:12px; background:#141b25; }
    .alias-header { display:flex; align-items:center; justify-content:space-between; flex-wrap:wrap; gap:8px; margin-bottom:9px; }
    .alias-header h3 { margin:0; color:var(--accent); }
    .list { list-style:none; padding:0; margin:0; display:grid; gap:0; }
    .list li { padding:7px 0; border-bottom:1px solid var(--line); display:flex; flex-wrap:wrap; align-items:center; gap:6px 14px; }
    .list li:last-child { border-bottom:0; }
    .group-title { font-size:11px; text-transform:uppercase; color:var(--muted); margin:12px 0 5px; letter-spacing:.5px; }
    .result,.status { white-space:pre-wrap; overflow-wrap:anywhere; color:var(--muted); font-size:13px; }
    .result:empty,.status:empty { display:none; }
    .empty { color:var(--muted); padding:14px 0; }
    .toolbar { display:flex; gap:8px 16px; flex-wrap:wrap; align-items:center; }
    .toolbar h2 { margin:0; margin-right:auto; }
    .toolbar #tracking-status,.toolbar #status { flex-basis:100%; }
    .room-stats { display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:10px; margin-bottom:18px; }
    .room-stat { padding:12px 14px; border:1px solid var(--line); border-radius:5px; background:#141b25; }
    .room-stat span { display:block; color:var(--muted); font-size:12px; }
    .room-stat strong { display:block; font-size:clamp(17px,1.8vw,24px); font-weight:600; font-variant-numeric:tabular-nums; margin-top:2px; overflow-wrap:anywhere; }
    .room-stat:last-child strong { color:var(--accent); }
    .tracker-filters { display:grid; grid-template-columns:minmax(160px,2fr) repeat(3,minmax(120px,1fr)); gap:10px; margin-bottom:12px; }
    .tracker-filter-footer { display:flex; align-items:center; justify-content:space-between; gap:10px; flex-wrap:wrap; margin:10px 0; }
    .inline-check { display:flex; align-items:center; gap:7px; }
    .table-wrap { overflow:auto; border:1px solid var(--line); border-radius:5px; }
    .tracker-table { width:100%; border-collapse:collapse; text-align:left; font-size:13px; }
    .tracker-table th { color:var(--muted); background:#202938; font-size:11px; font-weight:600; text-transform:uppercase; letter-spacing:.4px; padding:10px 12px; white-space:nowrap; }
    .tracker-table td { padding:10px 12px; border-top:1px solid var(--line); vertical-align:middle; }
    .tracker-table tbody tr:nth-child(even) { background:#1a222e; }
    .tracker-table tbody tr:hover { background:#222d3b; }
    .tracker-table td:first-child { font-weight:600; }
    .tracker-table .checks { min-width:145px; font-variant-numeric:tabular-nums; }
    .tracker-table .activity { color:var(--muted); white-space:nowrap; font-size:12px; }
    .tracker-table.rooms-table { min-width:0; }
    progress { display:block; height:5px; width:100%; margin-top:5px; border:0; border-radius:4px; overflow:hidden; background:#303c4c; accent-color:var(--accent); }
    progress::-webkit-progress-bar { background:#303c4c; }
    progress::-webkit-progress-value { background:var(--accent); }
    progress::-moz-progress-bar { background:var(--accent); }
    .state-label { border:1px solid #485061; border-radius:4px; display:inline-block; font-size:11px; padding:2px 7px; color:#c4cedb; white-space:nowrap; }
    .state-label.complete { color:#a6d1b0; background:#22372c; border-color:#3e614a; }
    .management-layout { grid-template-columns:repeat(2,minmax(0,1fr)); align-items:start; }
    .management-layout > .panel:first-child { grid-column:1/-1; }
    @media(min-width:1400px) { #recap-root,#hints-root,#items-root { grid-template-columns:repeat(2,minmax(0,1fr)); } }
    @media(max-width:1000px) { .tracker-filters { grid-template-columns:repeat(2,minmax(0,1fr)); } .management-layout { grid-template-columns:1fr; } }
    @media(max-width:760px) { body { grid-template-columns:minmax(0,1fr); } header { padding:16px; } .portal-nav { grid-column:1; flex-direction:row; padding:8px 12px; position:static; white-space:nowrap; border-bottom:1px solid var(--line); gap:2px; } .portal-nav a { padding:6px 8px; border-left:0; border-bottom:2px solid transparent; } .portal-nav a[aria-current] { border-bottom-color:var(--accent); } main { grid-column:1; padding:14px 12px 36px; gap:12px; } .room-stats { grid-template-columns:repeat(2,minmax(0,1fr)); } .tracker-table { min-width:680px; } .actions-grid { grid-template-columns:1fr; } .panel { padding:14px; } .title h1 { font-size:21px; } }
    @media(prefers-reduced-motion:reduce) { html { scroll-behavior:auto; } * { transition:none !important; } }
    """;

    public const string NavigationScript = """
    (() => {
      const main = document.querySelector('main');
      const nav = document.createElement('nav');
      nav.className = 'portal-nav';
      nav.setAttribute('aria-label', document.title);
      const panels = [...main.children].filter(node => node.classList.contains('panel'));
      const rebuild = () => {
      nav.replaceChildren();
      panels.forEach((panel, index) => {
        if (panel.hidden) return;
        const heading = panel.querySelector('h2');
        if (!heading) return;
        panel.id ||= 'portal-section-' + index;
        const link = document.createElement('a');
        link.href = '#' + panel.id;
        link.textContent = heading.textContent.replace(/[\u{1F000}-\u{1FFFF}\u{2600}-\u{27BF}\uFE0F]/gu, '').trim();
        link.addEventListener('click', () => {
          if (panel.tagName === 'DETAILS') panel.open = true;
          nav.querySelectorAll('a').forEach(item => item.removeAttribute('aria-current'));
          link.setAttribute('aria-current', 'location');
        });
        nav.appendChild(link);
        if (window.location.hash === '#' + panel.id) {
          if (panel.tagName === 'DETAILS') panel.open = true;
          link.setAttribute('aria-current', 'location');
        }
      });
      if (nav.firstChild && !nav.querySelector('[aria-current]')) nav.firstChild.setAttribute('aria-current', 'location');
      };
      main.before(nav);
      rebuild();
      new MutationObserver(rebuild).observe(main, {subtree:true, attributes:true, attributeFilter:['hidden']});
    })();
    """;
}
