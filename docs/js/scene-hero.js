/* scene-hero.js — SLOP facility overview hallucination
   Three.js particle scene for the landing page hero section.
   Renders a factory-complex shaped particle cloud with smoke,
   sparks, glitch effects, and scroll-driven degradation. */

import * as THREE from 'three';

(function () {
    'use strict';

    // -- bail if WebGL unavailable --
    const testCanvas = document.createElement('canvas');
    const gl = testCanvas.getContext('webgl') || testCanvas.getContext('experimental-webgl');
    if (!gl) return;

    const hero = document.querySelector('.hero');
    if (!hero) return;

    // -- reduced motion preference --
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    // -- create canvas --
    const canvas = document.createElement('canvas');
    canvas.className = 'hero-canvas';
    canvas.style.pointerEvents = 'none';
    hero.insertBefore(canvas, hero.querySelector('.hero-overlay'));

    // -- renderer --
    const renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: false });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setSize(hero.clientWidth, hero.clientHeight);

    // -- scene + camera --
    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(50, hero.clientWidth / hero.clientHeight, 0.1, 100);
    camera.position.set(0, 0, 20);

    // -- constants --
    const PARTICLE_COUNT = 500;
    const SMOKE_COUNT = 5;
    const TOTAL = PARTICLE_COUNT + SMOKE_COUNT;
    const ACCENT = new THREE.Color('#E8A031');
    const WHITE = new THREE.Color('#ffffff');
    const SMOKE_COLOR = new THREE.Color('#888888');

    // -- state --
    let degradeValue = 0;
    let mouseX = 0;
    let mouseY = 0;
    let glitchActive = false;
    let glitchTimer = scheduleGlitch();
    const clock = new THREE.Clock();

    // -- generate factory-complex home positions --
    // The factory shape: a wide rectangular cluster with a few taller
    // blocks (chimneys/towers) rising above, giving an industrial skyline feel.
    const homePositions = new Float32Array(TOTAL * 3);
    const velocities = new Float32Array(TOTAL * 3);

    for (let i = 0; i < PARTICLE_COUNT; i++) {
        const idx = i * 3;
        // decide which part of the factory this particle belongs to
        const r = Math.random();
        let x, y, z;

        if (r < 0.35) {
            // main building body — wide, low rectangle
            x = (Math.random() - 0.5) * 16;
            y = (Math.random() - 0.5) * 4 - 1;
            z = (Math.random() - 0.5) * 4;
        } else if (r < 0.55) {
            // left tower / chimney
            x = -5 + (Math.random() - 0.5) * 2;
            y = (Math.random()) * 5;
            z = (Math.random() - 0.5) * 2;
        } else if (r < 0.7) {
            // right tower
            x = 4 + (Math.random() - 0.5) * 2.5;
            y = (Math.random()) * 4;
            z = (Math.random() - 0.5) * 2;
        } else if (r < 0.85) {
            // center stack
            x = (Math.random() - 0.5) * 3;
            y = (Math.random()) * 6;
            z = (Math.random() - 0.5) * 2;
        } else {
            // scattered ground-level debris
            x = (Math.random() - 0.5) * 20;
            y = -2.5 + (Math.random() - 0.5) * 1.5;
            z = (Math.random() - 0.5) * 6;
        }

        homePositions[idx] = x;
        homePositions[idx + 1] = y;
        homePositions[idx + 2] = z;

        // slow drift velocities
        velocities[idx] = (Math.random() - 0.5) * 0.02;
        velocities[idx + 1] = (Math.random() - 0.5) * 0.02;
        velocities[idx + 2] = (Math.random() - 0.5) * 0.02;
    }

    // smoke particles — positioned above chimney/tower tops
    for (let i = PARTICLE_COUNT; i < TOTAL; i++) {
        const idx = i * 3;
        homePositions[idx] = -5 + (Math.random() - 0.5) * 1.5;
        homePositions[idx + 1] = 5 + Math.random() * 2;
        homePositions[idx + 2] = (Math.random() - 0.5) * 1;

        velocities[idx] = (Math.random() - 0.5) * 0.005;
        velocities[idx + 1] = 0.01 + Math.random() * 0.015;
        velocities[idx + 2] = (Math.random() - 0.5) * 0.005;
    }

    // -- build geometry --
    const geometry = new THREE.BufferGeometry();
    const positions = new Float32Array(TOTAL * 3);
    const colors = new Float32Array(TOTAL * 3);
    const sizes = new Float32Array(TOTAL);
    const alphas = new Float32Array(TOTAL);

    // copy home positions as starting positions
    positions.set(homePositions);

    // set initial colors and sizes
    for (let i = 0; i < PARTICLE_COUNT; i++) {
        colors[i * 3] = ACCENT.r;
        colors[i * 3 + 1] = ACCENT.g;
        colors[i * 3 + 2] = ACCENT.b;
        sizes[i] = 2.0 + Math.random() * 2.0;
        alphas[i] = 0.4 + Math.random() * 0.6;
    }

    for (let i = PARTICLE_COUNT; i < TOTAL; i++) {
        colors[i * 3] = SMOKE_COLOR.r;
        colors[i * 3 + 1] = SMOKE_COLOR.g;
        colors[i * 3 + 2] = SMOKE_COLOR.b;
        sizes[i] = 8.0 + Math.random() * 6.0;
        alphas[i] = 0.08 + Math.random() * 0.07;
    }

    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geometry.setAttribute('aColor', new THREE.BufferAttribute(colors, 3));
    geometry.setAttribute('aSize', new THREE.BufferAttribute(sizes, 1));
    geometry.setAttribute('aAlpha', new THREE.BufferAttribute(alphas, 1));

    // -- shader material --
    const material = new THREE.ShaderMaterial({
        transparent: true,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        uniforms: {
            uPixelRatio: { value: renderer.getPixelRatio() },
        },
        vertexShader: /* glsl */ `
            attribute float aSize;
            attribute float aAlpha;
            attribute vec3 aColor;
            varying float vAlpha;
            varying vec3 vColor;
            uniform float uPixelRatio;

            void main() {
                vAlpha = aAlpha;
                vColor = aColor;
                vec4 mvPosition = modelViewMatrix * vec4(position, 1.0);
                gl_PointSize = aSize * uPixelRatio * (10.0 / -mvPosition.z);
                gl_Position = projectionMatrix * mvPosition;
            }
        `,
        fragmentShader: /* glsl */ `
            varying float vAlpha;
            varying vec3 vColor;

            void main() {
                float d = length(gl_PointCoord - vec2(0.5));
                if (d > 0.5) discard;
                float glow = 1.0 - smoothstep(0.0, 0.5, d);
                glow = pow(glow, 1.5);
                gl_FragColor = vec4(vColor, vAlpha * glow);
            }
        `,
    });

    const points = new THREE.Points(geometry, material);
    scene.add(points);

    // -- spark flash state --
    let sparkIndex = -1;
    let sparkLife = 0;
    const sparkOrigAlpha = new Float32Array(1);
    const sparkOrigColor = new Float32Array(3);
    const sparkOrigSize = new Float32Array(1);

    // -- glitch scheduling --
    function scheduleGlitch() {
        if (prefersReducedMotion) return null;
        const delay = 8000 + Math.random() * 7000;
        return setTimeout(triggerGlitch, delay);
    }

    function triggerGlitch() {
        glitchActive = true;
        setTimeout(function () {
            glitchActive = false;
            glitchTimer = scheduleGlitch();
        }, 200);
    }

    // -- event listeners --
    window.addEventListener('slop-degrade', function (e) {
        degradeValue = e.detail.value;
    });

    window.addEventListener('mousemove', function (e) {
        mouseX = (e.clientX / window.innerWidth) * 2 - 1;
        mouseY = (e.clientY / window.innerHeight) * 2 - 1;
    });

    window.addEventListener('resize', function () {
        const w = hero.clientWidth;
        const h = hero.clientHeight;
        camera.aspect = w / h;
        camera.updateProjectionMatrix();
        renderer.setSize(w, h);
    });

    // -- animation loop --
    const speedMultiplier = prefersReducedMotion ? 0.2 : 1.0;

    function animate() {
        requestAnimationFrame(animate);

        const elapsed = clock.getElapsedTime() * speedMultiplier;
        const posAttr = geometry.getAttribute('position');
        const alphaAttr = geometry.getAttribute('aAlpha');
        const colorAttr = geometry.getAttribute('aColor');
        const sizeAttr = geometry.getAttribute('aSize');

        // chaos factor from scroll degradation
        const chaos = degradeValue * 8.0;

        for (let i = 0; i < PARTICLE_COUNT; i++) {
            const idx = i * 3;

            if (glitchActive) {
                // scatter randomly during glitch
                posAttr.array[idx] = (Math.random() - 0.5) * 30;
                posAttr.array[idx + 1] = (Math.random() - 0.5) * 20;
                posAttr.array[idx + 2] = (Math.random() - 0.5) * 10;
            } else {
                // drift around home position with chaos influence
                const driftX = Math.sin(elapsed * 0.3 + i * 0.1) * (0.3 + chaos * 0.5);
                const driftY = Math.cos(elapsed * 0.25 + i * 0.07) * (0.2 + chaos * 0.3);
                const driftZ = Math.sin(elapsed * 0.2 + i * 0.13) * (0.15 + chaos * 0.2);

                // scroll degradation pushes particles away from home
                const scatterX = (Math.sin(i * 7.3) * chaos);
                const scatterY = (Math.cos(i * 5.1) * chaos);
                const scatterZ = (Math.sin(i * 3.7) * chaos * 0.5);

                posAttr.array[idx] = homePositions[idx] + driftX + scatterX + velocities[idx] * elapsed;
                posAttr.array[idx + 1] = homePositions[idx + 1] + driftY + scatterY + velocities[idx + 1] * elapsed;
                posAttr.array[idx + 2] = homePositions[idx + 2] + driftZ + scatterZ + velocities[idx + 2] * elapsed;
            }
        }

        // smoke particles — slow upward drift, reset when too high
        for (let i = PARTICLE_COUNT; i < TOTAL; i++) {
            const idx = i * 3;
            posAttr.array[idx] += velocities[idx];
            posAttr.array[idx + 1] += velocities[idx + 1];
            posAttr.array[idx + 2] += velocities[idx + 2];

            // fade out as they rise, reset when faded
            const rise = posAttr.array[idx + 1] - homePositions[idx + 1];
            if (rise > 4) {
                posAttr.array[idx] = homePositions[idx] + (Math.random() - 0.5) * 0.5;
                posAttr.array[idx + 1] = homePositions[idx + 1];
                posAttr.array[idx + 2] = homePositions[idx + 2] + (Math.random() - 0.5) * 0.5;
            }
            alphaAttr.array[i] = Math.max(0, 0.12 - rise * 0.03);
        }

        // spark flash — pick a random particle, make it bright white briefly
        if (sparkIndex >= 0) {
            sparkLife -= 1;
            if (sparkLife <= 0) {
                // restore original values
                alphaAttr.array[sparkIndex] = sparkOrigAlpha[0];
                colorAttr.array[sparkIndex * 3] = sparkOrigColor[0];
                colorAttr.array[sparkIndex * 3 + 1] = sparkOrigColor[1];
                colorAttr.array[sparkIndex * 3 + 2] = sparkOrigColor[2];
                sizeAttr.array[sparkIndex] = sparkOrigSize[0];
                sizeAttr.needsUpdate = true;
                sparkIndex = -1;
            }
        }

        if (sparkIndex < 0 && Math.random() < 0.02) {
            sparkIndex = Math.floor(Math.random() * PARTICLE_COUNT);
            sparkLife = 3 + Math.floor(Math.random() * 5);
            // save originals
            sparkOrigAlpha[0] = alphaAttr.array[sparkIndex];
            sparkOrigColor[0] = colorAttr.array[sparkIndex * 3];
            sparkOrigColor[1] = colorAttr.array[sparkIndex * 3 + 1];
            sparkOrigColor[2] = colorAttr.array[sparkIndex * 3 + 2];
            sparkOrigSize[0] = sizeAttr.array[sparkIndex];
            // flash white + bright
            alphaAttr.array[sparkIndex] = 1.0;
            colorAttr.array[sparkIndex * 3] = WHITE.r;
            colorAttr.array[sparkIndex * 3 + 1] = WHITE.g;
            colorAttr.array[sparkIndex * 3 + 2] = WHITE.b;
            sizeAttr.array[sparkIndex] = sparkOrigSize[0] * 2.5;
            sizeAttr.needsUpdate = true;
        }

        posAttr.needsUpdate = true;
        alphaAttr.needsUpdate = true;
        colorAttr.needsUpdate = true;

        // mouse parallax — subtle camera shift
        const targetX = mouseX * 1.5;
        const targetY = mouseY * 1.0;
        camera.position.x += (targetX - camera.position.x) * 0.05;
        camera.position.y += (targetY - camera.position.y) * 0.05;
        camera.lookAt(0, 0, 0);

        renderer.render(scene, camera);
    }

    animate();
})();
