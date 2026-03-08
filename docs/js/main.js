/* Slopworks explainer site — shared JS */

(function () {
    'use strict';

    /* === NAVIGATION === */

    const NAV_PAGES = [
        { href: 'index.html', label: 'Home', id: 'index' },
        { href: 'story.html', label: 'Story', id: 'story' },
        { href: 'build.html', label: 'Build', id: 'build' },
        { href: 'explore.html', label: 'Explore', id: 'explore' },
        { href: 'tower.html', label: 'Tower', id: 'tower' },
        { href: 'slop.html', label: 'S.L.O.P.', id: 'slop' },
        { href: 'bible/', label: 'Bible', id: 'bible' },
    ];

    function buildNav() {
        var container = document.getElementById('nav');
        if (!container) return;

        // data-root allows bible subpages to prefix links back to docs root
        var rootPrefix = container.dataset.root || '';
        var currentPage = window.location.pathname.split('/').pop() || 'index.html';
        // Detect bible pages for active state
        var isBiblePage = window.location.pathname.indexOf('/bible') !== -1;

        var nav = document.createElement('nav');
        nav.className = 'site-nav';

        // Brand link
        var brand = document.createElement('a');
        brand.href = rootPrefix + 'index.html';
        brand.className = 'nav-brand';
        brand.textContent = 'SLOPWORKS';
        nav.appendChild(brand);

        // Hamburger button
        var hamburger = document.createElement('button');
        hamburger.className = 'nav-hamburger';
        hamburger.setAttribute('aria-label', 'Menu');
        for (var i = 0; i < 3; i++) {
            hamburger.appendChild(document.createElement('span'));
        }
        nav.appendChild(hamburger);

        // Links list
        var ul = document.createElement('ul');
        ul.className = 'nav-links';
        NAV_PAGES.forEach(function (p) {
            var li = document.createElement('li');
            var a = document.createElement('a');
            a.href = rootPrefix + p.href;
            a.textContent = p.label;
            var isActive = currentPage === p.href || (currentPage === '' && p.id === 'index');
            if (p.id === 'bible' && isBiblePage) isActive = true;
            if (isActive) {
                a.classList.add('active');
            }
            li.appendChild(a);
            ul.appendChild(li);
        });
        nav.appendChild(ul);

        container.appendChild(nav);

        // Hamburger toggles mobile menu
        hamburger.addEventListener('click', function (e) {
            e.stopPropagation();
            ul.classList.toggle('open');
        });
    }

    /* === FOOTER === */

    function buildFooter() {
        var container = document.getElementById('footer');
        if (!container) return;

        var footer = document.createElement('footer');
        footer.className = 'site-footer';

        var wrapper = document.createElement('div');
        wrapper.className = 'container';

        var corp = document.createElement('p');
        corp.className = 'corp-text';
        corp.textContent = 'Slopworks Industrial LLC. A Blackthorn Productions game.';
        wrapper.appendChild(corp);

        var status = document.createElement('p');
        status.className = 'build-status';
        status.textContent = 'S.L.O.P. v2.7.1 // STATUS: NOMINAL // UPTIME: \u2588\u2588\u2588\u2588 DAYS';
        wrapper.appendChild(status);

        footer.appendChild(wrapper);
        container.appendChild(footer);
    }

    /* === MOBILE NAV === */

    function initMobileNav() {
        // Close mobile nav on link click
        document.querySelectorAll('.nav-links a').forEach(function (link) {
            link.addEventListener('click', function () {
                var navLinks = document.querySelector('.nav-links');
                if (navLinks) {
                    navLinks.classList.remove('open');
                }
            });
        });

        // Close mobile nav on outside click
        document.addEventListener('click', function (e) {
            var navLinks = document.querySelector('.nav-links');
            if (!navLinks || !navLinks.classList.contains('open')) return;

            var nav = document.querySelector('.site-nav');
            if (nav && !nav.contains(e.target)) {
                navLinks.classList.remove('open');
            }
        });
    }

    /* === SCROLL REVEAL === */

    function initScrollReveal() {
        var sections = document.querySelectorAll('.section');
        if (!sections.length) return;

        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('visible');
                    observer.unobserve(entry.target);
                }
            });
        }, {
            threshold: 0.1,
            rootMargin: '0px 0px -50px 0px'
        });

        sections.forEach(function (s) { observer.observe(s); });
    }

    /* === REDACTED TEXT === */

    function initRedacted() {
        document.querySelectorAll('.redacted').forEach(function (el) {
            el.addEventListener('click', function () {
                el.classList.toggle('revealed');
            });
        });
    }

    /* === SLOP INTERJECTIONS === */

    function initInterjections() {
        var triggers = document.querySelectorAll('[data-slop-interjection]');
        if (!triggers.length) return;

        var notification = document.createElement('div');
        notification.className = 'slop-interjection';

        var textEl = document.createElement('p');
        textEl.className = 'interjection-text';
        notification.appendChild(textEl);

        document.body.appendChild(notification);

        var shown = new Set();
        var dismissTimer = null;

        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    var msg = entry.target.dataset.slopInterjection;
                    if (msg && !shown.has(msg)) {
                        shown.add(msg);
                        textEl.textContent = msg;
                        notification.classList.add('visible');

                        if (dismissTimer) clearTimeout(dismissTimer);
                        dismissTimer = setTimeout(function () {
                            notification.classList.remove('visible');
                        }, 6000);

                        observer.unobserve(entry.target);
                    }
                }
            });
        }, { threshold: 0.5 });

        triggers.forEach(function (el) { observer.observe(el); });
    }

    /* === SLOP GLITCH EFFECT === */

    function initGlitch() {
        var slopElements = document.querySelectorAll('.slop-says');
        if (!slopElements.length) return;

        function triggerGlitch() {
            var el = slopElements[Math.floor(Math.random() * slopElements.length)];
            el.classList.add('glitching');
            setTimeout(function () { el.classList.remove('glitching'); }, 300);
            var nextDelay = 5000 + Math.random() * 10000;
            setTimeout(triggerGlitch, nextDelay);
        }

        setTimeout(triggerGlitch, 3000);
    }

    /* === IMAGE FALLBACKS === */

    function initImageFallbacks() {
        document.querySelectorAll('.art-frame img').forEach(function (img) {
            img.addEventListener('error', function () {
                var placeholder = document.createElement('div');
                placeholder.className = 'art-placeholder';
                placeholder.textContent = '[IMAGE PENDING GENERATION]';
                img.parentElement.replaceChild(placeholder, img);
            });
        });
    }

    /* === PROGRESSIVE DEGRADATION === */

    function initProgressiveDegradation() {
        window.addEventListener('scroll', function () {
            var scrollHeight = document.documentElement.scrollHeight - window.innerHeight;
            var scrollPercent = scrollHeight > 0 ? window.scrollY / scrollHeight : 0;
            scrollPercent = Math.min(Math.max(scrollPercent, 0), 1);

            window.dispatchEvent(new CustomEvent('slop-degrade', {
                detail: { value: scrollPercent }
            }));
        }, { passive: true });
    }

    /* === INIT === */

    function init() {
        buildNav();
        buildFooter();
        initMobileNav();
        initScrollReveal();
        initRedacted();
        initInterjections();
        initGlitch();
        initImageFallbacks();
        initProgressiveDegradation();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
