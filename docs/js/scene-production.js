/* scene-production.js — SLOP production sim hallucination
   Three.js isometric factory scene for the build page.
   Renders machines, conveyor belts, and moving items with
   glitch effects and scroll-driven degradation. */

import * as THREE from 'three';

(function () {
    'use strict';

    // -- bail if WebGL unavailable --
    const testCanvas = document.createElement('canvas');
    const gl = testCanvas.getContext('webgl') || testCanvas.getContext('experimental-webgl');
    if (!gl) return;

    const container = document.getElementById('production-sim');
    if (!container) return;

    // -- reduced motion preference --
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const speedMultiplier = prefersReducedMotion ? 0.3 : 1.0;
    const glitchMultiplier = prefersReducedMotion ? 0.2 : 1.0;

    // -- colors --
    const COLORS = {
        bg: 0x0A0E14,
        floor: 0x12171F,
        smelter: 0xE8A031,
        assembler: 0x5CCFE6,
        storage: 0x4DBF6D,
        belt: 0x1A1F2A,
        beltItem: 0xE8A031,
        machineBase: 0x1A1F2A,
    };

    // -- create canvas --
    const canvas = document.createElement('canvas');
    canvas.style.pointerEvents = 'none';
    canvas.setAttribute('aria-hidden', 'true');
    container.appendChild(canvas);

    // -- add scan-lines overlay --
    const scanLines = document.createElement('div');
    scanLines.className = 'scan-lines';
    container.appendChild(scanLines);

    // -- add SLOP status readout --
    const statusReadout = document.createElement('div');
    statusReadout.style.cssText = [
        'position: absolute',
        'bottom: 0.75rem',
        'right: 0.75rem',
        'font-family: "Space Mono", monospace',
        'font-size: 0.6rem',
        'color: #6B7A8D',
        'text-transform: uppercase',
        'letter-spacing: 0.08em',
        'z-index: 5',
        'background: rgba(10, 14, 20, 0.7)',
        'padding: 0.25rem 0.5rem',
        'border-radius: 2px',
        'pointer-events: none',
    ].join(';');
    statusReadout.textContent = 'OUTPUT: NOMINAL // THROUGHPUT: 847 UNITS/HR';
    container.appendChild(statusReadout);

    // throughput fluctuation (uses wall clock, not scaled animation time)
    let lastThroughputUpdate = 0;
    const throughputInterval = 1000 + Math.random() * 1000;
    function updateThroughput(now) {
        if (now - lastThroughputUpdate > throughputInterval) {
            lastThroughputUpdate = now;
            const val = 820 + Math.floor(Math.random() * 60);
            statusReadout.textContent = 'OUTPUT: NOMINAL // THROUGHPUT: ' + val + ' UNITS/HR';
        }
    }

    // -- renderer --
    const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setClearColor(COLORS.bg, 1);

    // -- scene --
    const scene = new THREE.Scene();

    // -- isometric orthographic camera --
    // We'll size the camera frustum based on container aspect ratio
    const viewSize = 6;
    let aspect = container.clientWidth / container.clientHeight;

    const camera = new THREE.OrthographicCamera(
        -viewSize * aspect, viewSize * aspect,
        viewSize, -viewSize,
        0.1, 100
    );

    // isometric-ish angle: rotated 45 degrees on Y, tilted down ~35 degrees
    camera.position.set(10, 8, 10);
    camera.lookAt(0, 0, 0);

    function updateSize() {
        const w = container.clientWidth;
        const h = container.clientHeight;
        aspect = w / h;
        camera.left = -viewSize * aspect;
        camera.right = viewSize * aspect;
        camera.top = viewSize;
        camera.bottom = -viewSize;
        camera.updateProjectionMatrix();
        renderer.setSize(w, h);
    }
    updateSize();

    // -- floor --
    const floorGeo = new THREE.BoxGeometry(14, 0.15, 6);
    const floorMat = new THREE.MeshBasicMaterial({ color: COLORS.floor });
    const floor = new THREE.Mesh(floorGeo, floorMat);
    floor.position.set(0, -0.6, 0);
    scene.add(floor);

    // -- machines --
    function createMachine(x, topColor, label) {
        const group = new THREE.Group();
        group.position.set(x, 0, 0);

        // base cube
        const baseGeo = new THREE.BoxGeometry(1.8, 1.2, 1.8);
        const baseMat = new THREE.MeshBasicMaterial({ color: COLORS.machineBase });
        const base = new THREE.Mesh(baseGeo, baseMat);
        base.position.y = 0.1;
        group.add(base);

        // colored top
        const topGeo = new THREE.BoxGeometry(1.6, 0.25, 1.6);
        const topMat = new THREE.MeshBasicMaterial({
            color: topColor,
            transparent: true,
            opacity: 1.0,
        });
        const top = new THREE.Mesh(topGeo, topMat);
        top.position.y = 0.83;
        group.add(top);

        scene.add(group);

        return { group, baseMat, topMat, topMesh: top, label };
    }

    const smelter = createMachine(-4.5, COLORS.smelter, 'smelter');
    const assembler = createMachine(0, COLORS.assembler, 'assembler');
    const storage = createMachine(4.5, COLORS.storage, 'storage');
    const machines = [smelter, assembler, storage];

    // -- conveyor belts --
    function createBelt(startX, endX) {
        const length = endX - startX;
        const centerX = (startX + endX) / 2;
        const beltGeo = new THREE.BoxGeometry(length, 0.08, 0.8);
        const beltMat = new THREE.MeshBasicMaterial({ color: COLORS.belt });
        const belt = new THREE.Mesh(beltGeo, beltMat);
        belt.position.set(centerX, -0.5, 0);
        scene.add(belt);

        // belt rails (thin lines on each side)
        const railGeo = new THREE.BoxGeometry(length, 0.12, 0.05);
        const railMat = new THREE.MeshBasicMaterial({ color: 0x2A2F3A });
        const railL = new THREE.Mesh(railGeo, railMat);
        railL.position.set(centerX, -0.48, -0.4);
        scene.add(railL);
        const railR = new THREE.Mesh(railGeo, railMat);
        railR.position.set(centerX, -0.48, 0.4);
        scene.add(railR);

        return { startX, endX, length };
    }

    const belt1 = createBelt(-3.4, -1.1); // smelter -> assembler
    const belt2 = createBelt(1.1, 3.4);   // assembler -> storage

    // -- belt item pool --
    const ITEM_COUNT = 6;
    const itemGeo = new THREE.BoxGeometry(0.3, 0.3, 0.3);
    const itemMat = new THREE.MeshBasicMaterial({
        color: COLORS.beltItem,
        transparent: true,
        opacity: 1.0,
    });

    // each item tracks: which belt segment (0 or 1), progress along that segment (0-1)
    const items = [];
    for (let i = 0; i < ITEM_COUNT; i++) {
        const mesh = new THREE.Mesh(itemGeo, itemMat.clone());
        mesh.position.y = -0.3;
        scene.add(mesh);

        items.push({
            mesh,
            segment: 0,             // 0 = belt1, 1 = belt2
            progress: i / ITEM_COUNT, // stagger initial positions
            speed: 0.15 + Math.random() * 0.05,
            active: true,
        });
    }

    function getBeltPosition(segment, progress) {
        const belt = segment === 0 ? belt1 : belt2;
        const x = belt.startX + (belt.endX - belt.startX) * progress;
        return x;
    }

    // -- ghost machine state --
    let ghostMesh = null;
    let ghostLife = 0;
    let ghostTarget = null;

    function spawnGhost() {
        if (ghostMesh) return;
        const idx = Math.floor(Math.random() * machines.length);
        ghostTarget = machines[idx];

        const ghostGeo = new THREE.BoxGeometry(1.8, 1.2, 1.8);
        const ghostMat = new THREE.MeshBasicMaterial({
            color: COLORS.machineBase,
            transparent: true,
            opacity: 0.3,
        });
        ghostMesh = new THREE.Mesh(ghostGeo, ghostMat);

        const offset = (Math.random() - 0.5) * 1.2;
        ghostMesh.position.copy(ghostTarget.group.position);
        ghostMesh.position.x += offset;
        ghostMesh.position.z += (Math.random() - 0.5) * 0.8;
        ghostMesh.position.y = 0.1;

        scene.add(ghostMesh);
        ghostLife = 0.5; // seconds
    }

    // -- state --
    let degradeValue = 0;
    const clock = new THREE.Clock();

    // -- event listeners --
    window.addEventListener('slop-degrade', function (e) {
        degradeValue = e.detail.value;
    });

    window.addEventListener('resize', updateSize);

    // -- animation loop --
    function animate() {
        requestAnimationFrame(animate);

        const delta = clock.getDelta() * speedMultiplier;
        const elapsed = clock.getElapsedTime() * speedMultiplier;

        // throughput readout update (wall clock, unaffected by reduced motion)
        updateThroughput(performance.now());

        // glitch intensity scales with degradation
        const glitchChance = (0.1 + degradeValue * 0.4) * glitchMultiplier;

        // -- machine flicker --
        for (let i = 0; i < machines.length; i++) {
            const m = machines[i];
            if (Math.random() < glitchChance * 0.15) {
                // flicker opacity briefly
                m.topMat.opacity = 0.2 + Math.random() * 0.3;
            } else {
                // restore
                m.topMat.opacity += (1.0 - m.topMat.opacity) * 0.2;
            }
        }

        // -- ghost machine duplication --
        if (ghostMesh) {
            ghostLife -= delta;
            if (ghostLife <= 0) {
                scene.remove(ghostMesh);
                ghostMesh.geometry.dispose();
                ghostMesh.material.dispose();
                ghostMesh = null;
                ghostTarget = null;
            }
        } else if (Math.random() < (0.002 + degradeValue * 0.01) * glitchMultiplier) {
            spawnGhost();
        }

        // -- belt item movement --
        for (let i = 0; i < items.length; i++) {
            const item = items[i];
            if (!item.active) continue;

            // advance progress
            item.progress += item.speed * delta;

            // teleport glitch: randomly skip forward or backward
            if (Math.random() < (0.005 + degradeValue * 0.03) * glitchMultiplier) {
                item.progress += (Math.random() - 0.3) * 0.2;
                item.progress = Math.max(0, Math.min(1, item.progress));
            }

            // transition between belt segments
            if (item.progress >= 1.0) {
                if (item.segment === 0) {
                    // move to belt 2
                    item.segment = 1;
                    item.progress = 0;
                } else {
                    // reached storage — wrap back to smelter
                    item.segment = 0;
                    item.progress = 0;
                }
            }

            // update mesh position
            const x = getBeltPosition(item.segment, item.progress);
            item.mesh.position.x = x;
            item.mesh.position.z = 0;

            // slight bobbing
            item.mesh.position.y = -0.3 + Math.sin(elapsed * 3 + i * 1.5) * 0.02;

            // rotate items as they travel
            item.mesh.rotation.y += delta * 1.5;
        }

        renderer.render(scene, camera);
    }

    animate();
})();
