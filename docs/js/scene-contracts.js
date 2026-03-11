/* scene-contracts.js -- CRT contract board terminal animation
   Typewriter effect displaying available tower contracts in a
   retro terminal style. Loops continuously. No Three.js needed.

   Security: all innerHTML content is hardcoded string literals from
   CONTRACTS and threatColors. No user input, URL params, or external
   data is rendered. This matches the pattern in slop-terminal.js. */

(function () {
    'use strict';

    var terminal = document.getElementById('contract-terminal');
    if (!terminal) return;

    var output = document.getElementById('contract-output');
    if (!output) return;

    var CONTRACTS = [
        { id: 'TWR-001', name: '30 Rockefeller Plaza', tier: 1, threat: 'LOW', floors: 8, players: '1', status: 'AVAILABLE' },
        { id: 'TWR-002', name: 'MetLife Building', tier: 1, threat: 'MODERATE', floors: 12, players: '2', status: 'AVAILABLE' },
        { id: 'TWR-003', name: 'Woolworth Building', tier: 2, threat: 'HIGH', floors: 15, players: '2', status: 'LOCKED' },
        { id: 'TWR-004', name: 'One World Trade Center', tier: 2, threat: 'EXTREME', floors: 20, players: '4', status: 'LOCKED' },
    ];

    var threatColors = {
        LOW: '#5CCFE6',
        MODERATE: '#E8A031',
        HIGH: '#CC3333',
        EXTREME: '#CC3333',
    };

    // Build the line array
    function buildLines() {
        var lines = [];

        // System header
        lines.push({ text: '> SLOP CONTRACT MANAGEMENT SYSTEM v2.7.1', cls: 'system-text' });
        lines.push({ text: '> Loading active contracts...', cls: 'system-text' });
        lines.push({ text: '> Authorization: VERIFIED // Tier access: 1-2', cls: 'system-text' });

        // Contract entries
        for (var i = 0; i < CONTRACTS.length; i++) {
            var c = CONTRACTS[i];
            lines.push({ text: '\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500', cls: 'system-text' });
            lines.push({ text: 'CONTRACT ' + c.id + ': ' + c.name, cls: 'prompt-text' });
            lines.push({ text: '  Building: ' + c.name + ' // Floors: ' + c.floors + ' // Squad: ' + c.players, cls: 'response-text' });
            lines.push({
                text: '  Threat: <span style="color:' + threatColors[c.threat] + '">' + c.threat + '</span> // Status: ' + c.status,
                cls: 'response-text',
            });
        }

        // Footer
        lines.push({ text: '\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500', cls: 'system-text' });
        lines.push({ text: '> SLOP recommendation: Begin with TWR-001.', cls: 'system-text' });
        lines.push({ text: '> Estimated survival rate: 73%. This is acceptable.', cls: 'system-text' });

        return lines;
    }

    var prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var cancelled = false;

    // Type a single line character by character, HTML-aware
    function typeLine(div, text, callback) {
        var pos = 0;
        var rendered = '';

        function tick() {
            if (cancelled) return;
            if (pos >= text.length) {
                callback();
                return;
            }

            // If we hit an HTML tag, skip to closing >
            if (text[pos] === '<') {
                var closeIdx = text.indexOf('>', pos);
                if (closeIdx !== -1) {
                    rendered += text.substring(pos, closeIdx + 1);
                    pos = closeIdx + 1;
                    div.innerHTML = rendered;  // safe: hardcoded strings only
                    // Continue immediately, no delay for tags
                    tick();
                    return;
                }
            }

            rendered += text[pos];
            div.innerHTML = rendered;  // safe: hardcoded strings only
            pos++;

            // Variable speed
            var delay = 15 + Math.random() * 20;

            // Extra delay on punctuation
            var ch = text[pos - 1];
            if (ch === '.' || ch === ':' || ch === '\u2014') {
                delay += 80;
            }

            setTimeout(tick, delay);
        }

        tick();
    }

    // Show all lines immediately (reduced motion)
    function showAllImmediate() {
        var lines = buildLines();
        output.innerHTML = '';  // safe: clearing content
        for (var i = 0; i < lines.length; i++) {
            var div = document.createElement('div');
            div.className = 'line ' + lines[i].cls;
            div.innerHTML = lines[i].text;  // safe: hardcoded strings only
            output.appendChild(div);
        }
        output.scrollTop = output.scrollHeight;
    }

    // Animate lines one by one with typewriter effect
    function animateLines() {
        var lines = buildLines();
        output.innerHTML = '';  // safe: clearing content
        var lineIdx = 0;

        function nextLine() {
            if (cancelled) return;
            if (lineIdx >= lines.length) {
                // All lines done, wait 5s then restart
                setTimeout(function () {
                    if (!cancelled) animateLines();
                }, 5000);
                return;
            }

            var line = lines[lineIdx];
            lineIdx++;

            var div = document.createElement('div');
            div.className = 'line ' + line.cls;
            output.appendChild(div);
            output.scrollTop = output.scrollHeight;

            typeLine(div, line.text, function () {
                output.scrollTop = output.scrollHeight;
                nextLine();
            });
        }

        nextLine();
    }

    // Reduced motion: show everything, then loop by clearing and re-showing
    function loopImmediate() {
        if (cancelled) return;
        showAllImmediate();
        setTimeout(function () {
            if (!cancelled) loopImmediate();
        }, 5000);
    }

    // Start when terminal scrolls into view
    var started = false;

    var observer = new IntersectionObserver(function (entries) {
        for (var i = 0; i < entries.length; i++) {
            if (entries[i].isIntersecting && !started) {
                started = true;
                observer.disconnect();

                if (prefersReducedMotion) {
                    loopImmediate();
                } else {
                    animateLines();
                }
            }
        }
    }, { threshold: 0.3 });

    observer.observe(terminal);
})();
