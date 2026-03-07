/* Slopworks explainer site — shared JS */

(function () {
    'use strict';

    /* === NAVIGATION === */

    const NAV_PAGES = [
        { href: 'index.html', label: 'Home', id: 'index' },
        { href: 'assignment.html', label: 'Your assignment', id: 'assignment' },
        { href: 'facilities.html', label: 'Facilities', id: 'facilities' },
        { href: 'fauna.html', label: 'Fauna', id: 'fauna' },
        { href: 'colleagues.html', label: 'Colleagues', id: 'colleagues' },
        { href: 'slop.html', label: 'SLOP', id: 'slop' },
        { href: 'blueprints.html', label: 'Blueprints', id: 'blueprints' },
    ];

    function buildNav() {
        const container = document.getElementById('nav');
        if (!container) return;

        const currentPage = window.location.pathname.split('/').pop() || 'index.html';

        const nav = document.createElement('nav');
        nav.className = 'site-nav';

        // Brand link
        const brand = document.createElement('a');
        brand.href = 'index.html';
        brand.className = 'nav-brand';
        brand.textContent = 'SLOPWORKS ';
        const brandSub = document.createElement('span');
        brandSub.textContent = 'INDUSTRIAL // ORIENTATION';
        brand.appendChild(brandSub);
        nav.appendChild(brand);

        // Hamburger
        const hamburger = document.createElement('button');
        hamburger.className = 'nav-hamburger';
        hamburger.setAttribute('aria-label', 'Menu');
        hamburger.textContent = '\u2630';
        nav.appendChild(hamburger);

        // Links
        const ul = document.createElement('ul');
        ul.className = 'nav-links';
        NAV_PAGES.forEach(p => {
            const li = document.createElement('li');
            const a = document.createElement('a');
            a.href = p.href;
            a.textContent = p.label;
            if (currentPage === p.href || (currentPage === '' && p.id === 'index')) {
                a.className = 'active';
            }
            li.appendChild(a);
            ul.appendChild(li);
        });
        nav.appendChild(ul);

        container.appendChild(nav);

        hamburger.addEventListener('click', () => ul.classList.toggle('open'));
    }

    /* === FOOTER === */

    function buildFooter() {
        const container = document.getElementById('footer');
        if (!container) return;

        const footer = document.createElement('footer');
        footer.className = 'site-footer';

        const corp = document.createElement('p');
        corp.className = 'corp-text';
        corp.textContent = 'Slopworks Industrial LLC. All rights reserved. Unauthorized access to company systems is grounds for immediate reassignment.';
        footer.appendChild(corp);

        const status = document.createElement('p');
        status.className = 'build-status';
        status.textContent = 'SLOP v2.7.1 // BUILD STATUS: NOMINAL // UPTIME: \u2588\u2588\u2588\u2588 DAYS';
        footer.appendChild(status);

        container.appendChild(footer);
    }

    /* === SCROLL REVEAL === */

    function initScrollReveal() {
        const sections = document.querySelectorAll('.section');
        if (!sections.length) return;

        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    entry.target.classList.add('visible');
                    observer.unobserve(entry.target);
                }
            });
        }, {
            threshold: 0.1,
            rootMargin: '0px 0px -50px 0px'
        });

        sections.forEach(s => observer.observe(s));
    }

    /* === REDACTED TEXT === */

    function initRedacted() {
        document.querySelectorAll('.redacted').forEach(el => {
            el.addEventListener('click', () => el.classList.toggle('revealed'));
        });
    }

    /* === SLOP INTERJECTIONS === */

    function initInterjections() {
        const triggers = document.querySelectorAll('[data-slop-interjection]');
        if (!triggers.length) return;

        const container = document.createElement('div');
        container.className = 'slop-interjection scan-lines';

        const closeBtn = document.createElement('button');
        closeBtn.className = 'close-btn';
        closeBtn.textContent = '\u00d7';
        closeBtn.addEventListener('click', () => container.classList.remove('visible'));
        container.appendChild(closeBtn);

        const textEl = document.createElement('p');
        textEl.className = 'interjection-text';
        container.appendChild(textEl);

        document.body.appendChild(container);

        const shown = new Set();

        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const msg = entry.target.dataset.slopInterjection;
                    if (msg && !shown.has(msg)) {
                        shown.add(msg);
                        textEl.textContent = msg;
                        container.classList.add('visible');
                        setTimeout(() => container.classList.remove('visible'), 6000);
                        observer.unobserve(entry.target);
                    }
                }
            });
        }, { threshold: 0.5 });

        triggers.forEach(el => observer.observe(el));
    }

    /* === SLOP GLITCH EFFECT === */

    function initGlitch() {
        const slopElements = document.querySelectorAll('.slop-says');
        if (!slopElements.length) return;

        function triggerGlitch() {
            const el = slopElements[Math.floor(Math.random() * slopElements.length)];
            el.classList.add('glitching');
            setTimeout(() => el.classList.remove('glitching'), 300);
            setTimeout(triggerGlitch, 5000 + Math.random() * 10000);
        }

        setTimeout(triggerGlitch, 3000);
    }

    /* === THREAT METER (scroll-driven) === */

    function initThreatMeter() {
        const meter = document.querySelector('.threat-meter-fill');
        if (!meter) return;

        function update() {
            const scrollPct = window.scrollY / (document.body.scrollHeight - window.innerHeight);
            const clamped = Math.min(Math.max(scrollPct * 100, 0), 100);
            meter.style.width = clamped + '%';
        }

        window.addEventListener('scroll', update, { passive: true });
        update();
    }

    /* === IMAGE FALLBACK === */

    function initImageFallbacks() {
        document.querySelectorAll('.art-frame img').forEach(img => {
            img.addEventListener('error', () => {
                const placeholder = document.createElement('div');
                placeholder.className = 'art-placeholder';
                const label = document.createElement('span');
                label.textContent = '[IMAGE PENDING GENERATION]';
                placeholder.appendChild(label);
                img.parentElement.replaceChild(placeholder, img);
            });
        });
    }

    /* === INIT === */

    function init() {
        buildNav();
        buildFooter();
        initScrollReveal();
        initRedacted();
        initInterjections();
        initGlitch();
        initThreatMeter();
        initImageFallbacks();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
