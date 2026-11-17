/* Home page 3D hero: a rotating network globe with orbiting satellites (Three.js).
   If Three.js or WebGL is not available the CSS cube fallback is shown instead. */
(function () {
  var canvas = document.getElementById('hero3d');
  if (!canvas) return;
  var stage = canvas.parentElement;
  if (typeof THREE === 'undefined') { stage.classList.add('no-webgl'); return; }
  var renderer;
  try { renderer = new THREE.WebGLRenderer({ canvas: canvas, alpha: true, antialias: true }); }
  catch (e) { stage.classList.add('no-webgl'); return; }
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  var scene = new THREE.Scene(), camera = new THREE.PerspectiveCamera(45, 1, 0.1, 100);
  camera.position.z = 6.2;

  var globe = new THREE.Group(); scene.add(globe);
  // wireframe sphere
  var wire = new THREE.Mesh(new THREE.IcosahedronGeometry(1.9, 3), new THREE.MeshBasicMaterial({ color: 0x6366f1, wireframe: true, transparent: true, opacity: .35 }));
  globe.add(wire);
  // glowing core
  var core = new THREE.Mesh(new THREE.SphereGeometry(1.55, 32, 32), new THREE.MeshBasicMaterial({ color: 0x0b1030, transparent: true, opacity: .85 }));
  globe.add(core);
  // network nodes on the sphere + connecting lines
  var nodes = [], nodeGeo = new THREE.SphereGeometry(.055, 12, 12), nodeMat = new THREE.MeshBasicMaterial({ color: 0x67e8f9 });
  for (var i = 0; i < 46; i++) {
    var phi = Math.acos(2 * Math.random() - 1), th = Math.random() * Math.PI * 2, r = 1.92;
    var n = new THREE.Mesh(nodeGeo, nodeMat); n.position.set(r * Math.sin(phi) * Math.cos(th), r * Math.cos(phi), r * Math.sin(phi) * Math.sin(th));
    globe.add(n); nodes.push(n);
  }
  var lineMat = new THREE.LineBasicMaterial({ color: 0x06b6d4, transparent: true, opacity: .35 });
  for (var a = 0; a < nodes.length; a++) for (var b = a + 1; b < nodes.length; b++) {
    if (nodes[a].position.distanceTo(nodes[b].position) < 1.05) {
      var g = new THREE.BufferGeometry().setFromPoints([nodes[a].position, nodes[b].position]); globe.add(new THREE.Line(g, lineMat));
    }
  }
  // orbit rings with satellites
  var rings = [];
  [[2.7, 0x06b6d4, .5, .2], [3.2, 0xa855f7, -.35, -.9], [2.35, 0x6366f1, 1.1, .6]].forEach(function (cfg, idx) {
    var ring = new THREE.Mesh(new THREE.TorusGeometry(cfg[0], .012, 8, 120), new THREE.MeshBasicMaterial({ color: cfg[1], transparent: true, opacity: .55 }));
    ring.rotation.x = cfg[2]; ring.rotation.y = cfg[3];
    var sat = new THREE.Mesh(new THREE.OctahedronGeometry(.11), new THREE.MeshBasicMaterial({ color: cfg[1] }));
    ring.add(sat); rings.push({ ring: ring, sat: sat, radius: cfg[0], speed: .6 + idx * .25, phase: idx * 2 });
    scene.add(ring);
  });
  // star dust
  var starGeo = new THREE.BufferGeometry(), sp = [];
  for (var s = 0; s < 260; s++) sp.push((Math.random() - .5) * 14, (Math.random() - .5) * 9, (Math.random() - .5) * 8);
  starGeo.setAttribute('position', new THREE.Float32BufferAttribute(sp, 3));
  scene.add(new THREE.Points(starGeo, new THREE.PointsMaterial({ color: 0xaecbff, size: .03, transparent: true, opacity: .8 })));

  var mx = 0, my = 0;
  stage.addEventListener('mousemove', function (e) { var r = stage.getBoundingClientRect(); mx = (e.clientX - r.left) / r.width - .5; my = (e.clientY - r.top) / r.height - .5; });
  stage.addEventListener('mouseleave', function () { mx = my = 0; });
  function resize() { var w = stage.clientWidth, h = stage.clientHeight; renderer.setSize(w, h, false); camera.aspect = w / h; camera.updateProjectionMatrix(); }
  window.addEventListener('resize', resize); resize();

  var t = 0, still = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches, visible = true;
  document.addEventListener('visibilitychange', function () { visible = !document.hidden; if (visible) frame(); });
  function frame() {
    if (!visible) return;
    t += .008;
    if (!still) {
      globe.rotation.y += .0035;
      globe.rotation.x += (my * .6 - globe.rotation.x) * .04;
      globe.position.x += (mx * .5 - globe.position.x) * .04;
      rings.forEach(function (o) { o.ring.rotation.z += .002 * o.speed; o.sat.position.set(Math.cos(t * o.speed + o.phase) * o.radius, Math.sin(t * o.speed + o.phase) * o.radius, 0); });
      var pulse = 1 + Math.sin(t * 3) * .03; core.scale.set(pulse, pulse, pulse);
    }
    renderer.render(scene, camera);
    requestAnimationFrame(frame);
  }
  frame();
})();
