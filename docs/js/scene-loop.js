/* scene-loop.js -- Animated circuit-board flowchart
   Glowing orbs flow along paths between nodes, showing the
   Tower > Loot > Factory > Gear > (branches) > back to Tower loop.
   Nodes flash when orbs arrive. Runs on canvas, loops forever. */

(function () {
    'use strict';

    var canvas = document.getElementById('loop-canvas');
    if (!canvas) return;

    // ── Canvas setup ──
    var DPR = window.devicePixelRatio || 1;
    var W = 800;
    var H = 340;

    function sizeCanvas() {
        canvas.width = W * DPR;
        canvas.height = H * DPR;
        canvas.style.width = W + 'px';
        canvas.style.height = H + 'px';
        ctx = canvas.getContext('2d');
        ctx.scale(DPR, DPR);
    }

    var ctx;
    sizeCanvas();

    var TEAL = '#5CCFE6';
    var AMBER = '#E8A031';
    var WHITE = '#FFFFFF';

    // ── Nodes ──
    var NODES = [
        { x: 20,  y: 20,  w: 155, h: 44, label: 'Tower contracts' },
        { x: 230, y: 20,  w: 95,  h: 44, label: 'Loot' },
        { x: 380, y: 20,  w: 175, h: 44, label: 'Factory upgrades' },
        { x: 610, y: 20,  w: 155, h: 44, label: 'Better gear' },
        { x: 560, y: 155, w: 200, h: 44, label: 'Harder tower tiers' },
        { x: 300, y: 255, w: 230, h: 44, label: 'Wave defense + territory' },
    ];

    // Center helpers
    function cx(n) { return n.x + n.w / 2; }
    function cy(n) { return n.y + n.h / 2; }
    function right(n) { return n.x + n.w; }
    function bottom(n) { return n.y + n.h; }

    // ── Edges: each is an array of [x,y] waypoints ──
    var EDGES = [
        // 0: Tower -> Loot
        { pts: [[right(NODES[0]), cy(NODES[0])], [NODES[1].x, cy(NODES[1])]], color: AMBER },
        // 1: Loot -> Factory
        { pts: [[right(NODES[1]), cy(NODES[1])], [NODES[2].x, cy(NODES[2])]], color: AMBER },
        // 2: Factory -> Gear
        { pts: [[right(NODES[2]), cy(NODES[2])], [NODES[3].x, cy(NODES[3])]], color: TEAL },
        // 3: Gear -> Harder Towers (down-right)
        { pts: [[cx(NODES[3]), bottom(NODES[3])], [cx(NODES[3]), 120], [cx(NODES[4]), 120], [cx(NODES[4]), NODES[4].y]], color: AMBER },
        // 4: Factory -> Wave Defense (down)
        { pts: [[cx(NODES[2]), bottom(NODES[2])], [cx(NODES[2]), 200], [cx(NODES[5]), 200], [cx(NODES[5]), NODES[5].y]], color: TEAL },
        // 5: Harder Towers -> Tower (return loop, L-shape)
        { pts: [[NODES[4].x, cy(NODES[4])], [cx(NODES[0]), cy(NODES[4])], [cx(NODES[0]), bottom(NODES[0])]], color: AMBER, dashed: true },
    ];

    // ── Compute edge total lengths ──
    function pathLength(pts) {
        var len = 0;
        for (var i = 1; i < pts.length; i++) {
            var dx = pts[i][0] - pts[i - 1][0];
            var dy = pts[i][1] - pts[i - 1][1];
            len += Math.sqrt(dx * dx + dy * dy);
        }
        return len;
    }

    function pointAt(pts, t) {
        var total = pathLength(pts);
        var target = t * total;
        var walked = 0;
        for (var i = 1; i < pts.length; i++) {
            var dx = pts[i][0] - pts[i - 1][0];
            var dy = pts[i][1] - pts[i - 1][1];
            var segLen = Math.sqrt(dx * dx + dy * dy);
            if (walked + segLen >= target) {
                var frac = (target - walked) / segLen;
                return [pts[i - 1][0] + dx * frac, pts[i - 1][1] + dy * frac];
            }
            walked += segLen;
        }
        return pts[pts.length - 1];
    }

    // ── Orb (particle) system ──
    var orbs = [];

    function Orb(edgeIdx) {
        this.edge = edgeIdx;
        this.t = 0;
        this.speed = 0.012 + Math.random() * 0.004; // per frame at 60fps
        this.radius = 4 + Math.random() * 2;
        this.alive = true;
        this.color = EDGES[edgeIdx].color;
        this.trail = [];
    }

    // Spawn schedule: continuously cycle through edges
    var spawnQueue = [0, 1, 2, 3, 4, 5, 0, 2]; // weighted toward main flow
    var spawnIdx = 0;
    var spawnAccum = 0;
    var SPAWN_MS = 350;

    // ── Node flash state ──
    var nodeFlash = new Float32Array(NODES.length); // 0..1

    // ── Draw helpers ──

    function drawRoundRect(x, y, w, h, r) {
        ctx.moveTo(x + r, y);
        ctx.lineTo(x + w - r, y);
        ctx.arcTo(x + w, y, x + w, y + r, r);
        ctx.lineTo(x + w, y + h - r);
        ctx.arcTo(x + w, y + h, x + w - r, y + h, r);
        ctx.lineTo(x + r, y + h);
        ctx.arcTo(x, y + h, x, y + h - r, r);
        ctx.lineTo(x, y + r);
        ctx.arcTo(x, y, x + r, y, r);
        ctx.closePath();
    }

    function drawNode(n, idx) {
        var flash = nodeFlash[idx];

        ctx.save();

        // Glow when flashed
        if (flash > 0.05) {
            ctx.shadowColor = n.label.indexOf('Factory') >= 0 || n.label.indexOf('Wave') >= 0 || n.label.indexOf('Better') >= 0 ? TEAL : AMBER;
            ctx.shadowBlur = 6 + 20 * flash;
        }

        // Border
        var borderColor = n.label.indexOf('Factory') >= 0 || n.label.indexOf('Wave') >= 0 || n.label.indexOf('Better') >= 0 ? TEAL : AMBER;
        ctx.strokeStyle = borderColor;
        ctx.globalAlpha = 0.5 + 0.5 * flash;
        ctx.lineWidth = 1.5 + flash * 1.5;
        ctx.beginPath();
        drawRoundRect(n.x, n.y, n.w, n.h, 4);
        ctx.stroke();

        // Fill
        ctx.fillStyle = borderColor === AMBER
            ? 'rgba(232,160,49,' + (0.03 + 0.12 * flash) + ')'
            : 'rgba(92,207,230,' + (0.03 + 0.12 * flash) + ')';
        ctx.fill();

        ctx.restore();

        // Label
        ctx.save();
        ctx.globalAlpha = 0.65 + 0.35 * flash;
        ctx.fillStyle = borderColor;
        ctx.font = '12px "IBM Plex Sans", "Segoe UI", sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(n.label, n.x + n.w / 2, n.y + n.h / 2 + 1);
        ctx.restore();
    }

    function drawEdgeLine(e, time) {
        ctx.save();
        ctx.strokeStyle = e.color;
        ctx.lineWidth = 1;
        ctx.globalAlpha = 0.18;

        if (e.dashed) {
            ctx.setLineDash([5, 7]);
            ctx.lineDashOffset = -(time * 0.025);
            ctx.globalAlpha = 0.22;
        }

        ctx.beginPath();
        ctx.moveTo(e.pts[0][0], e.pts[0][1]);
        for (var i = 1; i < e.pts.length; i++) {
            ctx.lineTo(e.pts[i][0], e.pts[i][1]);
        }
        ctx.stroke();
        ctx.restore();

        // Arrowhead at end
        drawArrowhead(e);
    }

    function drawArrowhead(e) {
        var pts = e.pts;
        var tip = pts[pts.length - 1];
        var prev = pts[pts.length - 2];
        var angle = Math.atan2(tip[1] - prev[1], tip[0] - prev[0]);
        var sz = 7;

        ctx.save();
        ctx.globalAlpha = 0.3;
        ctx.fillStyle = e.color;
        ctx.beginPath();
        ctx.moveTo(tip[0], tip[1]);
        ctx.lineTo(tip[0] - sz * Math.cos(angle - 0.45), tip[1] - sz * Math.sin(angle - 0.45));
        ctx.lineTo(tip[0] - sz * Math.cos(angle + 0.45), tip[1] - sz * Math.sin(angle + 0.45));
        ctx.closePath();
        ctx.fill();
        ctx.restore();
    }

    function drawOrb(orb) {
        if (!orb.alive) return;
        var pos = pointAt(EDGES[orb.edge].pts, orb.t);

        // Trail: fading dots behind the orb
        for (var i = 0; i < orb.trail.length; i++) {
            var tp = orb.trail[i];
            var ratio = (i + 1) / orb.trail.length;
            var trailR = orb.radius * ratio * 0.6;
            var trailA = ratio * 0.35;

            ctx.save();
            ctx.globalAlpha = trailA;
            ctx.fillStyle = orb.color;
            ctx.shadowColor = orb.color;
            ctx.shadowBlur = trailR * 3;
            ctx.beginPath();
            ctx.arc(tp[0], tp[1], trailR, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();
        }

        // Outer glow
        ctx.save();
        ctx.globalAlpha = 0.6;
        ctx.fillStyle = orb.color;
        ctx.shadowColor = orb.color;
        ctx.shadowBlur = orb.radius * 6;
        ctx.beginPath();
        ctx.arc(pos[0], pos[1], orb.radius * 1.2, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();

        // Bright core
        ctx.save();
        ctx.globalAlpha = 0.95;
        ctx.fillStyle = WHITE;
        ctx.shadowColor = orb.color;
        ctx.shadowBlur = orb.radius * 3;
        ctx.beginPath();
        ctx.arc(pos[0], pos[1], orb.radius * 0.55, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
    }

    // ── Update ──

    function update(dt) {
        // Spawn
        spawnAccum += dt;
        while (spawnAccum >= SPAWN_MS) {
            spawnAccum -= SPAWN_MS;
            var eidx = spawnQueue[spawnIdx % spawnQueue.length];
            spawnIdx++;
            orbs.push(new Orb(eidx));
        }

        // Move orbs
        var dtFactor = dt / 16.67; // normalize to 60fps
        for (var i = orbs.length - 1; i >= 0; i--) {
            var o = orbs[i];
            var pos = pointAt(EDGES[o.edge].pts, o.t);
            o.trail.push([pos[0], pos[1]]);
            if (o.trail.length > 12) o.trail.shift();

            o.t += o.speed * dtFactor;

            if (o.t >= 1) {
                // Flash destination node
                var destIdx = [1, 2, 3, 4, 5, 0][o.edge]; // edge -> dest node
                nodeFlash[destIdx] = 1;
                o.alive = false;
                orbs.splice(i, 1);
            }
        }

        // Decay flashes
        for (var n = 0; n < nodeFlash.length; n++) {
            nodeFlash[n] *= (1 - 0.04 * dtFactor);
            if (nodeFlash[n] < 0.01) nodeFlash[n] = 0;
        }
    }

    // ── Render ──

    function render(time) {
        ctx.clearRect(0, 0, W, H);

        // Edge guide lines
        for (var e = 0; e < EDGES.length; e++) {
            drawEdgeLine(EDGES[e], time);
        }

        // Nodes
        for (var n = 0; n < NODES.length; n++) {
            drawNode(NODES[n], n);
        }

        // Orbs on top
        for (var o = 0; o < orbs.length; o++) {
            drawOrb(orbs[o]);
        }
    }

    // ── Main loop ──
    var running = false;
    var lastTime = 0;
    var rafId = null;

    function loop(timestamp) {
        if (!running) return;
        var dt = lastTime ? Math.min(timestamp - lastTime, 50) : 16;
        lastTime = timestamp;

        update(dt);
        render(timestamp);

        rafId = requestAnimationFrame(loop);
    }

    // ── Reduced motion ──
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        // Static: show all nodes, no particles
        for (var i = 0; i < nodeFlash.length; i++) nodeFlash[i] = 0.2;
        render(0);
        return;
    }

    // ── Start immediately, pause off-screen ──
    running = true;
    rafId = requestAnimationFrame(loop);

    new IntersectionObserver(function (entries) {
        if (entries[0].isIntersecting && !running) {
            running = true;
            lastTime = 0;
            rafId = requestAnimationFrame(loop);
        } else if (!entries[0].isIntersecting && running) {
            running = false;
            if (rafId) cancelAnimationFrame(rafId);
        }
    }, { threshold: 0.1 }).observe(canvas);

})();
