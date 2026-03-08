/* scene-elevator.js -- SLOP elevator shaft hallucination
   Three.js first-person elevator ride for the tower page.
   Renders a vertical shaft with floor beams, cables, floor number
   sprites, bioluminescent particles, and a creature shadow crossing
   mid-ride. Green night-vision tint via DOM overlay. Loops with
   static flash on reset. */

import * as THREE from 'three';

(function () {
    'use strict';

    // -- bail if WebGL unavailable --
    const testCanvas = document.createElement('canvas');
    const gl = testCanvas.getContext('webgl') || testCanvas.getContext('experimental-webgl');
    if (!gl) return;

    const container = document.getElementById('elevator-shaft');
    if (!container) return;

    // -- reduced motion preference --
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const speedMultiplier = prefersReducedMotion ? 0 : 1.0;

    // -- colors --
    const COLORS = {
        bg: 0x0A0E14,
        bgSurface: 0x12171F,
        wall: 0x1A1F2A,
        pipe: 0x2A2F3A,
        accent: 0xE8A031,
        particle: 0x5CCFE6,
        creature: 0x050505,
    };

    // -- create canvas --
    const canvas = document.createElement('canvas');
    canvas.style.pointerEvents = 'none';
    canvas.setAttribute('aria-hidden', 'true');
    container.appendChild(canvas);

    // -- green tint overlay --
    const greenOverlay = document.createElement('div');
    greenOverlay.style.cssText = [
        'background: rgba(0, 255, 65, 0.08)',
        'mix-blend-mode: multiply',
        'pointer-events: none',
        'position: absolute',
        'inset: 0',
        'z-index: 2',
    ].join(';');
    container.appendChild(greenOverlay);

    // -- grain overlay --
    const grainCanvas = document.createElement('canvas');
    grainCanvas.width = 128;
    grainCanvas.height = 128;
    grainCanvas.style.cssText = [
        'position: absolute',
        'inset: 0',
        'width: 100%',
        'height: 100%',
        'pointer-events: none',
        'z-index: 3',
        'opacity: 0.06',
        'mix-blend-mode: screen',
    ].join(';');
    container.appendChild(grainCanvas);
    const grainCtx = grainCanvas.getContext('2d');

    const grainImageData = grainCtx.createImageData(grainCanvas.width, grainCanvas.height);
    function updateGrain() {
        const data = grainImageData.data;
        for (let i = 0; i < data.length; i += 4) {
            const v = Math.random() * 255;
            data[i] = v;
            data[i + 1] = v;
            data[i + 2] = v;
            data[i + 3] = 255;
        }
        grainCtx.putImageData(grainImageData, 0, 0);
    }

    // -- static flash overlay (for loop reset) --
    const flashOverlay = document.createElement('div');
    flashOverlay.style.cssText = [
        'position: absolute',
        'inset: 0',
        'background: white',
        'pointer-events: none',
        'z-index: 4',
        'opacity: 0',
        'transition: opacity 0.05s',
    ].join(';');
    container.appendChild(flashOverlay);

    // -- renderer --
    const renderer = new THREE.WebGLRenderer({ canvas, antialias: false, alpha: false });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setClearColor(COLORS.bg, 1);

    // -- scene --
    const scene = new THREE.Scene();

    // -- camera --
    let aspect = container.clientWidth / container.clientHeight;
    const camera = new THREE.PerspectiveCamera(70, aspect, 0.1, 80);

    // -- shaft dimensions --
    const SHAFT_WIDTH = 3;
    const SHAFT_HEIGHT = 50;
    const FLOOR_COUNT = 12;
    const FLOOR_SPACING = 3;
    const HALF_W = SHAFT_WIDTH / 2;

    // -- build shaft walls (PlaneGeometry, facing inward) --
    const wallMat = new THREE.MeshBasicMaterial({ color: COLORS.wall, side: THREE.FrontSide });

    // front wall: z = -HALF_W, facing camera (toward +z)
    const frontWall = new THREE.Mesh(new THREE.PlaneGeometry(SHAFT_WIDTH, SHAFT_HEIGHT), wallMat);
    frontWall.position.set(0, SHAFT_HEIGHT / 2, -HALF_W);
    frontWall.rotation.y = Math.PI;
    scene.add(frontWall);

    // back wall: z = +HALF_W, facing toward -z (toward camera)
    const backWall = new THREE.Mesh(new THREE.PlaneGeometry(SHAFT_WIDTH, SHAFT_HEIGHT), wallMat);
    backWall.position.set(0, SHAFT_HEIGHT / 2, HALF_W);
    scene.add(backWall);

    // left wall: x = -HALF_W, facing toward +x
    const leftWall = new THREE.Mesh(new THREE.PlaneGeometry(SHAFT_WIDTH, SHAFT_HEIGHT), wallMat);
    leftWall.position.set(-HALF_W, SHAFT_HEIGHT / 2, 0);
    leftWall.rotation.y = Math.PI / 2;
    scene.add(leftWall);

    // right wall: x = +HALF_W, facing toward -x
    const rightWall = new THREE.Mesh(new THREE.PlaneGeometry(SHAFT_WIDTH, SHAFT_HEIGHT), wallMat);
    rightWall.position.set(HALF_W, SHAFT_HEIGHT / 2, 0);
    rightWall.rotation.y = -Math.PI / 2;
    scene.add(rightWall);

    // -- cross-beams at each floor level --
    const beamMat = new THREE.MeshBasicMaterial({ color: COLORS.pipe });

    for (let f = 0; f < FLOOR_COUNT; f++) {
        const beamY = f * FLOOR_SPACING + 1.5;
        const beam = new THREE.Mesh(
            new THREE.BoxGeometry(SHAFT_WIDTH, 0.15, 0.15),
            beamMat
        );
        beam.position.set(0, beamY, 0);
        scene.add(beam);
    }

    // -- vertical cables --
    const cablePositions = [-0.3, 0, 0.3];
    for (let c = 0; c < cablePositions.length; c++) {
        const cable = new THREE.Mesh(
            new THREE.BoxGeometry(0.04, SHAFT_HEIGHT, 0.04),
            beamMat
        );
        cable.position.set(cablePositions[c], SHAFT_HEIGHT / 2, 0);
        scene.add(cable);
    }

    // -- floor number sprites --
    for (let f = 1; f <= FLOOR_COUNT; f++) {
        const labelCanvas = document.createElement('canvas');
        labelCanvas.width = 64;
        labelCanvas.height = 64;
        const ctx = labelCanvas.getContext('2d');

        // dark background
        ctx.fillStyle = '#0A0E14';
        ctx.fillRect(0, 0, 64, 64);

        // amber text
        ctx.fillStyle = '#E8A031';
        ctx.font = 'bold 28px monospace';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(String(f), 32, 32);

        const tex = new THREE.CanvasTexture(labelCanvas);
        const labelMesh = new THREE.Mesh(
            new THREE.PlaneGeometry(0.6, 0.6),
            new THREE.MeshBasicMaterial({ map: tex, transparent: true })
        );
        labelMesh.position.set(0, f * FLOOR_SPACING, HALF_W - 0.05);
        scene.add(labelMesh);
    }

    // -- bioluminescent particles --
    const PARTICLE_COUNT = 15;
    const particleGeo = new THREE.BufferGeometry();
    const particlePositions = new Float32Array(PARTICLE_COUNT * 3);
    const particleHomePositions = new Float32Array(PARTICLE_COUNT * 3);

    for (let i = 0; i < PARTICLE_COUNT; i++) {
        const idx = i * 3;
        const x = (Math.random() * 2 - 1);
        const y = Math.random() * SHAFT_HEIGHT;
        const z = (Math.random() * 2 - 1);
        particleHomePositions[idx] = x;
        particleHomePositions[idx + 1] = y;
        particleHomePositions[idx + 2] = z;
        particlePositions[idx] = x;
        particlePositions[idx + 1] = y;
        particlePositions[idx + 2] = z;
    }

    particleGeo.setAttribute('position', new THREE.BufferAttribute(particlePositions, 3));

    // circular particle texture
    const particleCanvas = document.createElement('canvas');
    particleCanvas.width = 32;
    particleCanvas.height = 32;
    const pCtx = particleCanvas.getContext('2d');
    pCtx.beginPath();
    pCtx.arc(16, 16, 14, 0, Math.PI * 2);
    pCtx.fillStyle = '#ffffff';
    pCtx.fill();
    const particleTexture = new THREE.CanvasTexture(particleCanvas);

    const particleMat = new THREE.PointsMaterial({
        color: COLORS.particle,
        size: 0.12,
        map: particleTexture,
        transparent: true,
        opacity: 0.7,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        sizeAttenuation: true,
    });

    const particles = new THREE.Points(particleGeo, particleMat);
    scene.add(particles);

    // -- creature shadow --
    const creatureGeo = new THREE.PlaneGeometry(1.5, 0.8);
    const creatureMat = new THREE.MeshBasicMaterial({
        color: COLORS.creature,
        transparent: true,
        opacity: 0.85,
    });
    const creature = new THREE.Mesh(creatureGeo, creatureMat);
    creature.rotation.x = -Math.PI / 2;
    creature.visible = false;
    scene.add(creature);

    // creature animation state
    let creatureTriggered = false;
    let creatureActive = false;
    let creatureCrossProgress = 0;
    const CREATURE_START_X = -2;
    const CREATURE_END_X = 2;
    const CREATURE_CROSS_SPEED = 3.0;

    // -- timing --
    const CYCLE_TIME = 10.0;
    const CAMERA_START_Y = 1;
    const CAMERA_END_Y = FLOOR_COUNT * FLOOR_SPACING; // floor 12 = y:36
    const CREATURE_TRIGGER_PROGRESS = 0.6;

    // -- scene label --
    const label = container.querySelector('.scene-label');

    // -- state --
    let loopTime = 0;
    let flashActive = false;
    const clock = new THREE.Clock();

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

    window.addEventListener('resize', updateSize);

    // -- trigger creature cross --
    function triggerCreature(cameraY) {
        if (creatureActive) return;
        creatureActive = true;
        creatureCrossProgress = 0;
        creature.visible = true;
        creature.position.set(CREATURE_START_X, cameraY + 2, 0);
    }

    // -- static flash for loop reset --
    function triggerFlash() {
        if (flashActive) return;
        flashActive = true;
        flashOverlay.style.opacity = '0.8';
        setTimeout(function () {
            flashOverlay.style.opacity = '0';
            flashActive = false;
        }, 100);
    }

    // -- grain update interval (throttled) --
    let lastGrainUpdate = 0;
    const GRAIN_INTERVAL = 80;

    // -- animation loop --
    function animate() {
        requestAnimationFrame(animate);

        const delta = Math.min(clock.getDelta(), 0.1) * speedMultiplier;
        const elapsed = clock.getElapsedTime() * speedMultiplier;

        // advance loop time
        loopTime += delta;

        // grain update (throttled)
        const now = performance.now();
        if (now - lastGrainUpdate > GRAIN_INTERVAL) {
            lastGrainUpdate = now;
            updateGrain();
        }

        // -- camera ride up the shaft --
        const loopProgress = Math.min(loopTime / CYCLE_TIME, 1.0);
        const cameraY = CAMERA_START_Y + (CAMERA_END_Y - CAMERA_START_Y) * loopProgress;
        camera.position.set(0, cameraY, 0);
        camera.lookAt(0, cameraY + 10, 0);

        // -- current floor for label --
        const currentFloor = Math.max(1, Math.min(FLOOR_COUNT, Math.ceil(cameraY / FLOOR_SPACING)));
        if (label) {
            label.textContent = 'S.L.O.P.://ELEVATOR_CAM [FLOOR ' + currentFloor + ']';
        }

        // -- creature trigger at 60% progress --
        if (loopProgress >= CREATURE_TRIGGER_PROGRESS && !creatureTriggered) {
            creatureTriggered = true;
            triggerCreature(cameraY);
        }

        // -- creature cross animation --
        if (creatureActive) {
            creatureCrossProgress += CREATURE_CROSS_SPEED * delta;
            const t = creatureCrossProgress / (CREATURE_END_X - CREATURE_START_X);
            creature.position.x = CREATURE_START_X + (CREATURE_END_X - CREATURE_START_X) * Math.min(t, 1.0);

            if (t >= 1.0) {
                creature.visible = false;
                creatureActive = false;
            }
        }

        // -- bioluminescent particles: slow float --
        const posAttr = particleGeo.getAttribute('position');
        for (let i = 0; i < PARTICLE_COUNT; i++) {
            const idx = i * 3;
            const driftX = Math.sin(elapsed * 0.4 + i * 2.1) * 0.15;
            const driftY = Math.cos(elapsed * 0.3 + i * 1.7) * 0.1;
            const driftZ = Math.sin(elapsed * 0.25 + i * 3.3) * 0.08;

            posAttr.array[idx] = particleHomePositions[idx] + driftX;
            posAttr.array[idx + 1] = particleHomePositions[idx + 1] + driftY;
            posAttr.array[idx + 2] = particleHomePositions[idx + 2] + driftZ;
        }
        posAttr.needsUpdate = true;

        // particle glow pulse
        particleMat.opacity = 0.5 + Math.sin(elapsed * 1.2) * 0.2;

        // -- loop reset --
        if (loopProgress >= 1.0) {
            triggerFlash();
            loopTime = 0;
            creatureTriggered = false;
        }

        renderer.render(scene, camera);
    }

    animate();
})();
