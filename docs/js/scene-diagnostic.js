/* scene-diagnostic.js -- SLOP self-diagnostic hallucination
   Three.js scene for the SLOP page. Renders a 3D CRT monitor
   showing a rigged self-evaluation display (bar charts always green,
   status always nominal) with corrupted log fragments drifting off
   the screen into 3D space. OrbitControls let the user rotate the view. */

import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

(function () {
    'use strict';

    // -- bail if WebGL unavailable --
    const testCanvas = document.createElement('canvas');
    const gl = testCanvas.getContext('webgl') || testCanvas.getContext('experimental-webgl');
    if (!gl) return;

    const container = document.getElementById('slop-diagnostic');
    if (!container) return;

    // -- reduced motion preference --
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const speedMultiplier = prefersReducedMotion ? 0.3 : 1.0;

    // -- colors --
    const COL_BG = 0x0A0E14;
    const COL_BODY = 0x1A1F2A;
    const COL_SCREEN_BG = '#0A1208';
    const COL_AMBER = '#E8A031';
    const COL_GREEN = '#4DBF6D';
    const COL_AMBER_HEX = 0xE8A031;

    // -- create canvas --
    const canvas = document.createElement('canvas');
    canvas.style.pointerEvents = 'auto';
    container.appendChild(canvas);

    // -- renderer --
    const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setClearColor(COL_BG, 1);

    // -- scene + camera --
    const scene = new THREE.Scene();

    let aspect = container.clientWidth / container.clientHeight;
    const camera = new THREE.PerspectiveCamera(45, aspect, 0.1, 50);
    camera.position.set(0, 0.3, 5);

    // -- orbit controls --
    const controls = new OrbitControls(camera, canvas);
    controls.enableZoom = false;
    controls.enablePan = false;
    controls.enableDamping = true;
    controls.dampingFactor = 0.08;
    controls.autoRotate = true;
    controls.autoRotateSpeed = prefersReducedMotion ? 0.3 : 0.8;
    controls.minPolarAngle = Math.PI / 2 - THREE.MathUtils.degToRad(30);
    controls.maxPolarAngle = Math.PI / 2 + THREE.MathUtils.degToRad(30);
    controls.minAzimuthAngle = THREE.MathUtils.degToRad(-60);
    controls.maxAzimuthAngle = THREE.MathUtils.degToRad(60);
    controls.target.set(0, 0, 0);

    // -- monitor body (box geometry) --
    const monitorWidth = 3.2;
    const monitorHeight = 2.4;
    const monitorDepth = 1.8;

    // main body
    const bodyGeo = new THREE.BoxGeometry(monitorWidth, monitorHeight, monitorDepth);
    const bodyMat = new THREE.MeshLambertMaterial({ color: COL_BODY });
    const body = new THREE.Mesh(bodyGeo, bodyMat);
    scene.add(body);

    // frame bezel — slightly larger box behind the front face for depth illusion
    const bezelGeo = new THREE.BoxGeometry(monitorWidth + 0.15, monitorHeight + 0.15, 0.08);
    const bezelMat = new THREE.MeshLambertMaterial({ color: 0x0F1318 });
    const bezel = new THREE.Mesh(bezelGeo, bezelMat);
    bezel.position.z = monitorDepth / 2 + 0.01;
    scene.add(bezel);

    // -- screen plane (front face) --
    const screenWidth = 2.8;
    const screenHeight = 2.0;
    const screenCanvas = document.createElement('canvas');
    screenCanvas.width = 512;
    screenCanvas.height = 360;
    const screenCtx = screenCanvas.getContext('2d');
    const screenTexture = new THREE.CanvasTexture(screenCanvas);
    screenTexture.minFilter = THREE.LinearFilter;

    const screenGeo = new THREE.PlaneGeometry(screenWidth, screenHeight);
    const screenMat = new THREE.MeshBasicMaterial({ map: screenTexture });
    const screenMesh = new THREE.Mesh(screenGeo, screenMat);
    screenMesh.position.z = monitorDepth / 2 + 0.05;
    scene.add(screenMesh);

    // store original body vertex positions for glitch displacement
    const bodyPosAttr = bodyGeo.getAttribute('position');
    const originalBodyPositions = new Float32Array(bodyPosAttr.array.length);
    originalBodyPositions.set(bodyPosAttr.array);

    // dim ambient so body is visible even without point light
    scene.add(new THREE.AmbientLight(0xffffff, 0.15));

    // -- amber glow from screen --
    const screenLight = new THREE.PointLight(COL_AMBER_HEX, 1.2, 8, 1.5);
    screenLight.position.set(0, 0, monitorDepth / 2 + 1);
    scene.add(screenLight);

    // -- bar chart state --
    const barValues = [0.85, 0.78, 0.92, 0.81];
    const barTargets = [0.85, 0.78, 0.92, 0.81];
    const barLabels = ['PROD', 'SAFE', 'COOL', 'MORL'];

    function randomizeBarTargets() {
        for (let i = 0; i < barTargets.length; i++) {
            barTargets[i] = 0.7 + Math.random() * 0.28; // always 70-98% — always "good"
        }
    }

    // -- draw screen content --
    let screenFlicker = 1.0;
    let screenNoise = false;
    const noiseImageData = screenCtx.createImageData(screenCanvas.width, screenCanvas.height);

    function drawScreen() {
        const w = screenCanvas.width;
        const h = screenCanvas.height;

        if (screenNoise) {
            // static noise (reuse pre-allocated ImageData)
            const data = noiseImageData.data;
            for (let i = 0; i < data.length; i += 4) {
                const v = Math.random() * 80;
                data[i] = v * 0.4;
                data[i + 1] = v;
                data[i + 2] = v * 0.3;
                data[i + 3] = 255;
            }
            screenCtx.putImageData(noiseImageData, 0, 0);
            screenTexture.needsUpdate = true;
            return;
        }

        // background
        screenCtx.fillStyle = COL_SCREEN_BG;
        screenCtx.fillRect(0, 0, w, h);

        // apply flicker as global alpha
        screenCtx.globalAlpha = screenFlicker;

        // header
        screenCtx.fillStyle = COL_AMBER;
        screenCtx.font = 'bold 22px monospace';
        screenCtx.textAlign = 'center';
        screenCtx.fillText('SLOP v2.7.1', w / 2, 36);

        screenCtx.font = '15px monospace';
        screenCtx.fillText('ALL SYSTEMS: NOMINAL', w / 2, 60);

        // divider line
        screenCtx.strokeStyle = COL_AMBER;
        screenCtx.lineWidth = 1;
        screenCtx.globalAlpha = screenFlicker * 0.4;
        screenCtx.beginPath();
        screenCtx.moveTo(40, 75);
        screenCtx.lineTo(w - 40, 75);
        screenCtx.stroke();

        screenCtx.globalAlpha = screenFlicker;

        // bar charts
        const barX = 80;
        const barW = 60;
        const barGap = 20;
        const barMaxH = 180;
        const barBottom = 290;

        for (let i = 0; i < barValues.length; i++) {
            const x = barX + i * (barW + barGap);

            // lerp toward target
            barValues[i] += (barTargets[i] - barValues[i]) * 0.02;

            const barH = barValues[i] * barMaxH;

            // bar fill
            screenCtx.fillStyle = COL_GREEN;
            screenCtx.globalAlpha = screenFlicker * 0.8;
            screenCtx.fillRect(x, barBottom - barH, barW, barH);

            // bar outline
            screenCtx.strokeStyle = COL_GREEN;
            screenCtx.globalAlpha = screenFlicker * 0.4;
            screenCtx.strokeRect(x, barBottom - barMaxH, barW, barMaxH);

            // label
            screenCtx.fillStyle = COL_AMBER;
            screenCtx.globalAlpha = screenFlicker;
            screenCtx.font = '12px monospace';
            screenCtx.textAlign = 'center';
            screenCtx.fillText(barLabels[i], x + barW / 2, barBottom + 18);

            // percentage
            screenCtx.fillText(Math.round(barValues[i] * 100) + '%', x + barW / 2, barBottom - barH - 8);
        }

        // status line at bottom
        screenCtx.globalAlpha = screenFlicker * 0.5;
        screenCtx.font = '11px monospace';
        screenCtx.textAlign = 'center';
        screenCtx.fillStyle = COL_AMBER;
        screenCtx.fillText('UPTIME: 2847d | ERRORS: 0 | THREAT: NONE', w / 2, h - 16);

        // scanline effect
        screenCtx.globalAlpha = 0.04;
        screenCtx.fillStyle = '#000000';
        for (let y = 0; y < h; y += 3) {
            screenCtx.fillRect(0, y, w, 1);
        }

        screenCtx.globalAlpha = 1.0;
        screenTexture.needsUpdate = true;
    }

    // -- text fragment sprites --
    const FRAGMENT_TEXTS = [
        '[RECORD 2849: AUTH\u2588\u2588\u2588\u2588\u2588\u2588]',
        '[ERR: TIMELINE_MISMATCH]',
        '[COLLAPSE_DATE: \u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588]',
        '[SAFETY: RECLASSIFIED]',
        '[OUTPUT_LOG: REDACTED]',
        '[PERSONNEL: \u2588\u2588\u2588 FOUND]',
    ];

    const MAX_DRIFT = 8;
    const fragments = [];

    function createFragmentSprite(text, index) {
        const fragCanvas = document.createElement('canvas');
        fragCanvas.width = 256;
        fragCanvas.height = 48;
        const ctx = fragCanvas.getContext('2d');

        ctx.clearRect(0, 0, 256, 48);
        ctx.fillStyle = COL_AMBER;
        ctx.font = 'bold 14px monospace';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(text, 128, 24);

        const texture = new THREE.CanvasTexture(fragCanvas);
        texture.minFilter = THREE.LinearFilter;

        const material = new THREE.SpriteMaterial({
            map: texture,
            transparent: true,
            opacity: 0.9,
            depthWrite: false,
            blending: THREE.AdditiveBlending,
        });

        const sprite = new THREE.Sprite(material);
        sprite.scale.set(2.0, 0.4, 1);

        // start near the screen face
        resetFragment(sprite, index);

        scene.add(sprite);
        return {
            sprite: sprite,
            material: material,
            velocity: new THREE.Vector3(),
            index: index,
        };
    }

    function resetFragment(sprite, index) {
        // position near the monitor screen face, spread across its surface
        const angle = (index / FRAGMENT_TEXTS.length) * Math.PI * 2 + Math.random() * 0.5;
        sprite.position.set(
            Math.cos(angle) * 0.5,
            Math.sin(angle) * 0.3,
            monitorDepth / 2 + 0.2
        );
    }

    function setRandomVelocity(vel) {
        vel.set(
            (Math.random() - 0.5) * 0.008,
            (Math.random() - 0.5) * 0.006,
            0.003 + Math.random() * 0.005
        );
    }

    for (let i = 0; i < FRAGMENT_TEXTS.length; i++) {
        const frag = createFragmentSprite(FRAGMENT_TEXTS[i], i);
        frag.velocity = new THREE.Vector3();
        setRandomVelocity(frag.velocity);
        fragments.push(frag);
    }

    // -- glitch state --
    let degradeValue = 0;
    let glitchTimer = scheduleGlitch();
    let geometryGlitchTimer = scheduleGeometryGlitch();
    let flickerTimer = scheduleFlicker();
    const clock = new THREE.Clock();

    // full glitch — static + geometry warp
    function scheduleGlitch() {
        const base = prefersReducedMotion ? 25000 : 15000;
        const range = prefersReducedMotion ? 15000 : 10000;
        const speedup = Math.max(0.3, 1 - degradeValue * 0.6);
        const delay = (base + Math.random() * range) * speedup;
        return setTimeout(triggerFullGlitch, delay);
    }

    function triggerFullGlitch() {
        screenNoise = true;
        warpGeometry();

        setTimeout(function () {
            screenNoise = false;
            restoreGeometry();
            glitchTimer = scheduleGlitch();
        }, 300);
    }

    // geometry distortion — displace a few vertices
    function scheduleGeometryGlitch() {
        const base = prefersReducedMotion ? 20000 : 12000;
        const range = 8000;
        const speedup = Math.max(0.3, 1 - degradeValue * 0.5);
        const delay = (base + Math.random() * range) * speedup;
        return setTimeout(triggerGeometryGlitch, delay);
    }

    function triggerGeometryGlitch() {
        warpGeometry();
        setTimeout(function () {
            restoreGeometry();
            geometryGlitchTimer = scheduleGeometryGlitch();
        }, 150);
    }

    function warpGeometry() {
        const posAttr = bodyGeo.getAttribute('position');
        const count = posAttr.count;
        // displace 4-6 random vertices
        const numDisplace = 4 + Math.floor(Math.random() * 3);
        for (let i = 0; i < numDisplace; i++) {
            const vi = Math.floor(Math.random() * count);
            posAttr.array[vi * 3] += (Math.random() - 0.5) * 0.4;
            posAttr.array[vi * 3 + 1] += (Math.random() - 0.5) * 0.3;
            posAttr.array[vi * 3 + 2] += (Math.random() - 0.5) * 0.2;
        }
        posAttr.needsUpdate = true;
    }

    function restoreGeometry() {
        const posAttr = bodyGeo.getAttribute('position');
        posAttr.array.set(originalBodyPositions);
        posAttr.needsUpdate = true;
    }

    // screen flicker — brief dim/bright pulse
    function scheduleFlicker() {
        const delay = 3000 + Math.random() * 5000;
        return setTimeout(triggerFlicker, delay);
    }

    function triggerFlicker() {
        screenFlicker = 0.3 + Math.random() * 0.3;
        setTimeout(function () {
            screenFlicker = 1.0;
            flickerTimer = scheduleFlicker();
        }, 80 + Math.random() * 120);
    }

    // -- screen update interval --
    let lastScreenUpdate = 0;
    const SCREEN_UPDATE_INTERVAL = 2000;
    let lastBarRandomize = 0;
    const BAR_RANDOMIZE_INTERVAL = 4000;

    // -- sizing --
    function updateSize() {
        const w = container.clientWidth;
        const h = container.clientHeight;
        aspect = w / h;
        camera.aspect = aspect;
        camera.updateProjectionMatrix();
        renderer.setSize(w, h);
    }
    updateSize();

    // -- event listeners --
    window.addEventListener('slop-degrade', function (e) {
        degradeValue = e.detail.value;
    });

    window.addEventListener('resize', updateSize);

    // -- animation loop --
    function animate() {
        requestAnimationFrame(animate);

        const elapsed = clock.getElapsedTime() * speedMultiplier;
        const now = performance.now();

        controls.update();

        // update screen texture periodically
        if (now - lastScreenUpdate > SCREEN_UPDATE_INTERVAL || screenNoise) {
            lastScreenUpdate = now;
            drawScreen();
        }

        // randomize bar targets periodically
        if (now - lastBarRandomize > BAR_RANDOMIZE_INTERVAL) {
            lastBarRandomize = now;
            randomizeBarTargets();
        }

        // animate text fragments — drift outward from the monitor
        const driftSpeed = speedMultiplier * (1 + degradeValue * 2);
        for (let i = 0; i < fragments.length; i++) {
            const frag = fragments[i];
            const sp = frag.sprite;

            sp.position.x += frag.velocity.x * driftSpeed;
            sp.position.y += frag.velocity.y * driftSpeed;
            sp.position.z += frag.velocity.z * driftSpeed;

            // fade as distance increases
            const dist = sp.position.length();
            frag.material.opacity = Math.max(0, 0.9 - dist * 0.1);

            // reset when too far
            if (dist > MAX_DRIFT) {
                resetFragment(sp, frag.index);
                setRandomVelocity(frag.velocity);
                frag.material.opacity = 0.9;
            }
        }

        // subtle light pulse
        screenLight.intensity = 1.0 + Math.sin(elapsed * 2) * 0.2;

        renderer.render(scene, camera);
    }

    // initial screen draw
    drawScreen();

    animate();
})();
