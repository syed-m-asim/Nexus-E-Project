/* ==========================================================================
   Nexus Communication - UI behaviour (no framework needed)
   1 theme toggle  2 progress bar  3 particle network background  4 3D card tilt
   5 scroll reveal  6 animated counters  7 button ripple  8 status badges
   9 table search + sorting  10 active menu link  11 typing effect
   Everything is progressive: if this file fails the pages still work.
   ========================================================================== */
(function () {
  'use strict';
  var root = document.documentElement;
  var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  var finePointer = window.matchMedia && window.matchMedia('(hover: hover) and (pointer: fine)').matches;
  function $$(sel, ctx) { return Array.prototype.slice.call((ctx || document).querySelectorAll(sel)); }

  /* 1. Theme toggle (dark / light), remembered in localStorage ------------------------- */
  function applyTheme(t) {
    root.setAttribute('data-bs-theme', t);
    // every theme button (header on phones + menu on desktop) shows the icon of the mode you can switch TO
    $$('.theme-toggle i').forEach(function (icon) { icon.className = t === 'dark' ? 'bi bi-sun-fill' : 'bi bi-moon-stars-fill'; });
  }
  var toggles = $$('.theme-toggle');
  if (toggles.length) {
    applyTheme(root.getAttribute('data-bs-theme') || 'dark');
    toggles.forEach(function (btn) {
      btn.addEventListener('click', function () {
        var next = root.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
        try { localStorage.setItem('nx-theme', next); } catch (e) { }
        applyTheme(next);
      });
    });
  }

  /* 2. Top progress bar while navigating ------------------------------------------------- */
  var bar = document.getElementById('nx-progress');
  function startBar() { if (!bar) return; bar.style.opacity = 1; bar.style.width = '70%'; }
  document.addEventListener('click', function (e) {
    var a = e.target.closest && e.target.closest('a[href]');
    if (a && a.host === location.host && !a.target && !a.hasAttribute('data-bs-toggle') && a.getAttribute('href').charAt(0) !== '#') startBar();
  });
  window.addEventListener('beforeunload', startBar);
  window.addEventListener('pageshow', function () { if (bar) { bar.style.width = '100%'; setTimeout(function () { bar.style.opacity = 0; bar.style.width = 0; }, 250); } });

  /* 3. Particle network background (canvas) ------------------------------------------------ */
  (function particles() {
    var c = document.getElementById('bg-canvas');
    if (!c || reduceMotion) return;
    var ctx = c.getContext('2d'), w, h, pts = [], mouse = { x: -999, y: -999 }, running = true;
    function size() { w = c.width = window.innerWidth; h = c.height = window.innerHeight; var n = Math.min(90, Math.floor(w * h / 18000)); pts = []; for (var i = 0; i < n; i++) pts.push({ x: Math.random() * w, y: Math.random() * h, vx: (Math.random() - .5) * .45, vy: (Math.random() - .5) * .45, r: Math.random() * 1.6 + .6 }); }
    size(); window.addEventListener('resize', size);
    window.addEventListener('mousemove', function (e) { mouse.x = e.clientX; mouse.y = e.clientY; });
    document.addEventListener('visibilitychange', function () { running = !document.hidden; if (running) loop(); });
    function loop() {
      if (!running) return;
      ctx.clearRect(0, 0, w, h);
      var dark = root.getAttribute('data-bs-theme') !== 'light', col = dark ? '125,160,255' : '67,56,202';
      for (var i = 0; i < pts.length; i++) {
        var p = pts[i]; p.x += p.vx; p.y += p.vy;
        if (p.x < 0 || p.x > w) p.vx *= -1; if (p.y < 0 || p.y > h) p.vy *= -1;
        var dx = p.x - mouse.x, dy = p.y - mouse.y, d = Math.sqrt(dx * dx + dy * dy);
        if (d < 120) { p.x += dx / d * 1.2; p.y += dy / d * 1.2; }
        ctx.beginPath(); ctx.arc(p.x, p.y, p.r, 0, 6.283); ctx.fillStyle = 'rgba(' + col + ',.8)'; ctx.fill();
        for (var j = i + 1; j < pts.length; j++) {
          var q = pts[j], ex = p.x - q.x, ey = p.y - q.y, dist = ex * ex + ey * ey;
          if (dist < 14000) { ctx.strokeStyle = 'rgba(' + col + ',' + (0.22 * (1 - dist / 14000)) + ')'; ctx.lineWidth = 1; ctx.beginPath(); ctx.moveTo(p.x, p.y); ctx.lineTo(q.x, q.y); ctx.stroke(); }
        }
      }
      requestAnimationFrame(loop);
    }
    loop();
  })();

  /* 4. 3D tilt + glare on cards (mouse only; not on cards that contain forms or tables) ------- */
  if (finePointer && !reduceMotion) {
    $$('.card').forEach(function (card) {
        if (card.matches('form, table') || card.querySelector('form, table, .no-tilt') || card.classList.contains('no-tilt')) return;
      card.addEventListener('mousemove', function (e) {
        var r = card.getBoundingClientRect(), x = e.clientX - r.left, y = e.clientY - r.top;
        card.style.setProperty('--ry', ((x / r.width - .5) * 14).toFixed(2) + 'deg');
        card.style.setProperty('--rx', ((.5 - y / r.height) * 14).toFixed(2) + 'deg');
        card.style.setProperty('--mx', x + 'px'); card.style.setProperty('--my', y + 'px');
      });
      card.addEventListener('mouseleave', function () { card.style.setProperty('--rx', '0deg'); card.style.setProperty('--ry', '0deg'); });
    });
      $$('.card form, .card table, form.card, table.card').forEach(function (el) {
          var card = el.closest('.card'); if (card) { card.classList.add('no-tilt'); }
      });
    $$('.card').forEach(function (card) {   // glare only for form/table cards
      if (!card.classList.contains('no-tilt')) return;
      card.addEventListener('mousemove', function (e) { var r = card.getBoundingClientRect(); card.style.setProperty('--mx', (e.clientX - r.left) + 'px'); card.style.setProperty('--my', (e.clientY - r.top) + 'px'); });
    });
  }

  /* 4b. Page header: heading on the left, all "Add / New / Place / Upload" buttons on the right end ---------- */
  $$('main h2').forEach(function (h) {
    var btns = $$('a.btn, button.btn', h);
    if (!btns.length || h.closest('.page-head')) return;
    var head = document.createElement('div'); head.className = 'page-head';
    var actions = document.createElement('div'); actions.className = 'page-actions';
    btns.forEach(function (b) {
      var t = b.textContent.trim();
      if (!b.querySelector('i')) {
        var icon = /^(add|new|place|record|generate|upload)/i.test(t) ? 'bi-plus-lg' : 'bi-arrow-right-circle';
        b.insertAdjacentHTML('afterbegin', '<i class="bi ' + icon + '"></i>');
      }
      b.classList.remove('btn-sm'); actions.appendChild(b);
    });
    h.parentNode.insertBefore(head, h); head.appendChild(h); head.appendChild(actions);
  });

  /* 5. Scroll reveal (staggered) -------------------------------------------------------------- */
  var revealEls = $$('main .card, main h2, main > table, main > .table-wrap, main dl, main .alert, main .row > [class*="col-"] > .card, .hero > *, .section-title');
  revealEls = revealEls.filter(function (el, i, arr) { return arr.indexOf(el) === i; });
  if ('IntersectionObserver' in window && !reduceMotion) {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) {
        if (en.isIntersecting) {
          var el = en.target; el.classList.add('in'); io.unobserve(el);
          // after the entrance animation hand control back to normal CSS (so the 3D tilt reacts quickly)
          setTimeout(function () { el.classList.remove('reveal', 'in'); el.style.removeProperty('--d'); }, 1100);
        }
      });
    }, { threshold: .08, rootMargin: '0px 0px -40px 0px' });
    revealEls.forEach(function (el, i) { el.classList.add('reveal'); el.style.setProperty('--d', Math.min(i % 8, 7) * 0.06 + 's'); io.observe(el); });
  }

  /* 6. Animated counters for dashboard numbers ------------------------------------------------- */
  function countUp(el) {
    var txt = el.textContent.trim(), m = txt.match(/^([^\d\-]*)(-?[\d,]*\.?\d+)(.*)$/);
    if (!m || /[A-Za-z]{2,}/.test(txt)) return;
    var target = parseFloat(m[2].replace(/,/g, '')), dec = (m[2].split('.')[1] || '').length, grouped = m[2].indexOf(',') > -1;
    if (isNaN(target) || target === 0) return;
    var t0 = null, dur = 1200;
    function fmt(v) { var s = v.toFixed(dec); if (grouped) { var p = s.split('.'); p[0] = p[0].replace(/\B(?=(\d{3})+(?!\d))/g, ','); s = p.join('.'); } return m[1] + s + m[3]; }
    function step(ts) { if (!t0) t0 = ts; var k = Math.min((ts - t0) / dur, 1), e = 1 - Math.pow(1 - k, 3); el.textContent = fmt(target * e); if (k < 1) requestAnimationFrame(step); else el.textContent = txt; }
    requestAnimationFrame(step);
  }
  if (!reduceMotion) {
    var cio = 'IntersectionObserver' in window ? new IntersectionObserver(function (es) { es.forEach(function (en) { if (en.isIntersecting) { countUp(en.target); cio.unobserve(en.target); } }); }, { threshold: .4 }) : null;
    $$('.card-body > h3, .card-body > h2, [data-count]').forEach(function (el) { if (cio) cio.observe(el); });
  }

  /* 7. Ripple on buttons ------------------------------------------------------------------------ */
  document.addEventListener('click', function (e) {
    var b = e.target.closest && e.target.closest('.btn');
    if (!b || reduceMotion) return;
    var r = b.getBoundingClientRect(), s = Math.max(r.width, r.height), sp = document.createElement('span');
    sp.className = 'ripple'; sp.style.width = sp.style.height = s + 'px';
    sp.style.left = (e.clientX - r.left - s / 2) + 'px'; sp.style.top = (e.clientY - r.top - s / 2) + 'px';
    b.appendChild(sp); setTimeout(function () { sp.remove(); }, 650);
  });

  /* 8. Colour the status badges by their text ----------------------------------------------------- */
  var okW = /^(active|paid|feasible|connectionprovided|received|provided|completed|yes|true|online)/i;
  var warnW = /^(partiallypaid|partiallyprovided|feasibilityinprogress|temporarilyinactive|ordered|pending|placed)/i;
  var badW = /^(unpaid|notfeasible|permanentlyinactive|cancelled|overdue|inactive|no|false)/i;
  $$('.badge').forEach(function (b) {
    var t = b.textContent.replace(/\s+/g, '');
    b.classList.add(okW.test(t) ? 'b-ok' : warnW.test(t) ? 'b-warn' : badW.test(t) ? 'b-bad' : /^\d/.test(t) ? 'b-info' : 'b-info');
  });

  /* 8b. Turn plain links in the last column of tables into action buttons (Details / Edit / Delete ...) ------ */
  $$('table.table').forEach(function (tbl) {
    var ths = $$('thead th', tbl); if (!ths.length) return;
    var last = ths[ths.length - 1], idx = ths.length - 1, hasLinks = false;
    Array.prototype.slice.call(tbl.tBodies[0] ? tbl.tBodies[0].rows : []).forEach(function (r) {
      var td = r.cells[idx]; if (!td) return;
      var links = $$('a', td);
      if (!links.length) return;
      hasLinks = true;
      var wrap = document.createElement('div'); wrap.className = 'actions';
      links.forEach(function (a) {
        var t = a.textContent.trim().toLowerCase();
        var kind = /delete|remove/.test(t) ? 'act-delete' : /edit|update/.test(t) ? 'act-edit' : /detail|view|open|bill|pay/.test(t) ? 'act-view' : 'act-other';
        var icon = kind === 'act-delete' ? 'bi-trash' : kind === 'act-edit' ? 'bi-pencil-square' : kind === 'act-view' ? 'bi-eye' : 'bi-arrow-right-circle';
        a.className = 'act-btn ' + kind;
        a.insertAdjacentHTML('afterbegin', '<i class="bi ' + icon + '"></i>');
        wrap.appendChild(a);
      });
      Array.prototype.slice.call(td.childNodes).forEach(function (n) { if (n.nodeType === 3 || (n.nodeType === 1 && n.tagName !== 'FORM')) { if (n !== wrap && !wrap.contains(n)) td.removeChild(n); } });
      td.insertBefore(wrap, td.firstChild);
    });
    if (hasLinks && !last.textContent.trim()) { last.textContent = 'Actions'; last.classList.add('th-actions'); }
  });

  /* 9. Table search + click-to-sort (tables with more than 5 rows) --------------------------------- */
  $$('table.table').forEach(function (tbl) {
    var body = tbl.tBodies[0]; if (!body) return;
    var rows = Array.prototype.slice.call(body.rows);
    var wrap = document.createElement('div'); wrap.className = 'table-wrap';
    tbl.parentNode.insertBefore(wrap, tbl); wrap.appendChild(tbl);
    if (rows.length < 6) return;
    var tools = document.createElement('div'); tools.className = 'table-tools';
    tools.innerHTML = '<input type="search" class="form-control form-control-sm" placeholder="Search in table..." /><span class="table-count"></span>';
    wrap.parentNode.insertBefore(tools, wrap);
    var input = tools.querySelector('input'), count = tools.querySelector('.table-count');
    function update() { var q = input.value.toLowerCase(), n = 0; rows.forEach(function (r) { var show = r.textContent.toLowerCase().indexOf(q) > -1; r.style.display = show ? '' : 'none'; if (show) n++; }); count.textContent = n + ' of ' + rows.length + ' rows'; }
    input.addEventListener('input', update); update();
    $$('thead th', tbl).forEach(function (th, idx) {
      if (!th.textContent.trim() || th.classList.contains('th-actions')) return;
      th.classList.add('sortable');
      th.addEventListener('click', function () {
        var asc = !th.classList.contains('sort-asc');
        $$('thead th', tbl).forEach(function (x) { x.classList.remove('sort-asc', 'sort-desc'); });
        th.classList.add(asc ? 'sort-asc' : 'sort-desc');
        rows.sort(function (a, b) {
          var x = (a.cells[idx] || {}).textContent || '', y = (b.cells[idx] || {}).textContent || '';
          var nx = parseFloat(x.replace(/[^0-9.\-]/g, '')), ny = parseFloat(y.replace(/[^0-9.\-]/g, ''));
          var r = (!isNaN(nx) && !isNaN(ny) && /\d/.test(x) && /\d/.test(y)) ? nx - ny : x.trim().localeCompare(y.trim());
          return asc ? r : -r;
        });
        rows.forEach(function (r) { body.appendChild(r); });
      });
    });
  });

  /* 9b. Phones: every table row becomes a small card. Each cell gets its column heading as a label (CSS does the rest) ---- */
  $$('table.table').forEach(function (tbl) {
    var heads = $$('thead th', tbl).map(function (th) { return th.textContent.trim(); });
    if (!heads.length || !tbl.tBodies[0]) return;
    tbl.classList.add('table-stack');
    Array.prototype.slice.call(tbl.tBodies[0].rows).forEach(function (r) {
      var col = 0;
      Array.prototype.slice.call(r.cells).forEach(function (td) {
        td.setAttribute('data-label', td.colSpan > 1 ? '' : (heads[col] || ''));
        col += td.colSpan || 1;
      });
    });
  });

  /* 10. Highlight the active menu link ------------------------------------------------------------- */
  $$('.nx-navbar .nav-link[href]').forEach(function (a) {
    var p = a.getAttribute('href'); if (p === '/' ? location.pathname === '/' : (p !== '#' && location.pathname.toLowerCase().indexOf(p.toLowerCase()) === 0)) a.classList.add('active');
  });

  /* 11. Typing effect for the hero sentence ----------------------------------------------------------- */
  var typed = document.querySelector('[data-typing]');
  if (typed) {
    var phrases = (typed.getAttribute('data-typing') || '').split('|'), pi = 0, ci = 0, del = false;
    if (reduceMotion) { typed.textContent = phrases[0]; }
    else (function tick() {
      var cur = phrases[pi]; ci += del ? -1 : 1; typed.textContent = cur.substring(0, ci);
      var wait = del ? 35 : 70;
      if (!del && ci === cur.length) { del = true; wait = 1600; } else if (del && ci === 0) { del = false; pi = (pi + 1) % phrases.length; wait = 400; }
      setTimeout(tick, wait);
    })();
  }
})();


/* Password show/hide toggle (works in Chrome, Edge, Firefox etc.) */
function setupPasswordToggle(btnId, inputId, iconId) {
    var btn = document.getElementById(btnId);
    var input = document.getElementById(inputId);
    var icon = document.getElementById(iconId);
    if (!btn || !input || !icon) return;

    btn.addEventListener('click', function () {
        if (input.type === 'password') {
            input.type = 'text';
            icon.classList.remove('bi-eye');
            icon.classList.add('bi-eye-slash');
        } else {
            input.type = 'password';
            icon.classList.remove('bi-eye-slash');
            icon.classList.add('bi-eye');
        }
    });
}

setupPasswordToggle('toggleLoginPassword', 'loginPassword', 'loginEyeIcon');
setupPasswordToggle('toggleRegPassword', 'regPassword', 'regEyeIcon');
setupPasswordToggle('toggleRegConfirmPassword', 'regConfirmPassword', 'regConfirmEyeIcon');
setupPasswordToggle('toggleEmployeePassword', 'employeePassword', 'employeeEyeIcon');
setupPasswordToggle('toggleCpCurrent', 'cpCurrentPassword', 'cpCurrentEye');
setupPasswordToggle('toggleCpNew', 'cpNewPassword', 'cpNewEye');
setupPasswordToggle('toggleCpConfirm', 'cpConfirmPassword', 'cpConfirmEye');