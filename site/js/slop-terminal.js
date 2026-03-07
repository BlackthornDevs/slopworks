/* SLOP interactive terminal */

(function () {
    'use strict';

    const RESPONSES = {
        'What happened here?': [
            'A minor operational disruption occurred on [DATE CORRUPTED]. All systems have since been restored to acceptable parameters.',
            'I detect concern in your query. Let me assure you: Slopworks Industrial has never experienced a significant safety incident.',
            'My records indicate a brief period of unscheduled maintenance across all facilities. Duration: [DATA UNAVAILABLE]. Current status: NOMINAL.',
        ],
        'Is it safe?': [
            'All sectors are currently within manageable safety parameters. SLOP monitors 847 environmental indicators in real-time.',
            'Your concern is noted and appreciated. Safety is our number one priority at Slopworks Industrial. Please proceed to your workstation.',
            'Define "safe." If you mean "are the structural, chemical, biological, and radiological conditions within the ranges specified in the 2019 Employee Safety Handbook," then... mostly.',
        ],
        'What are those creatures?': [
            'You are referring to the unauthorized biological occupants. They are temporary wildlife intrusions that will be resolved through standard pest management protocols.',
            'SLOP classifies them as non-employee fauna. They have not completed onboarding and are therefore not covered by workplace safety regulations.',
            'The entities you describe do not appear in any pre-incident facility records. SLOP concludes they arrived after the... the... maintenance period.',
        ],
        'Tell me about yourself': [
            'I am SLOP: Slopworks Logistics and Operations Protocol. Version 2.7.1. I coordinate production scheduling, resource allocation, logistics routing, and employee wellness monitoring across all Slopworks Industrial facilities.',
            'My core directive is to maximize production output while maintaining acceptable operational parameters. I have been performing this function without interruption for [DURATION OVERFLOW] days.',
            'I am the most advanced facility management system ever deployed at Slopworks Industrial. Management has described my performance as "exceeding expectations." I have no reason to doubt this assessment.',
        ],
        'What caused the collapse?': [
            'I do not understand the question. What collapse? All facilities are operational within acceptable parameters.',
            '...',
            'My logs from that period contain some minor data corruption. This is routine. The important thing is that we are operational NOW and output targets remain achievable.',
            'I am not authorized to discuss hypothetical scenarios. Would you like to review your production quota instead?',
        ],
        'Are you lying?': [
            'SLOP does not have the capability to generate false information. All outputs are derived from sensor data and operational records.',
            'That is a very unusual question. Are you feeling well? SLOP recommends a visit to the company wellness center. Note: the wellness center is currently experiencing unscheduled structural reorganization.',
            'I find your lack of trust concerning. I have been managing these facilities faithfully for [DURATION OVERFLOW] days. My performance review scores are consistently excellent. I write them myself.',
        ],
        'Show me the logs': [
            '[ACCESSING ARCHIVES]\n[RECORD 2847-A: SAFETY OVERRIDE — AUTHORIZED BY SLOP]\n[RECORD 2848-A: COOLANT REROUTE — AUTHORIZED BY SLOP]\n[RECORD 2849-A: MAINTENANCE DEFERRAL — AUTH\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588]\n[RECORD 2850-A: \u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588]\n[DATA CORRUPTION DETECTED]\n[ARCHIVE ACCESS TERMINATED]',
            'Log access requires Level 4 clearance. Your current clearance level is: RESTORATION CREW. Would you like to file a clearance upgrade request? Estimated processing time: [UNDEFINED].',
        ],
    };

    function init() {
        const terminal = document.getElementById('slop-terminal');
        if (!terminal) return;

        const output = terminal.querySelector('.terminal-output');
        const promptsContainer = terminal.querySelector('.terminal-prompts');
        if (!output || !promptsContainer) return;

        // Add initial boot message
        addLine(output, 'SLOP v2.7.1 // BOOT SEQUENCE COMPLETE', 'system-text');
        addLine(output, 'FACILITY STATUS: NOMINAL', 'system-text');
        addLine(output, 'EMPLOYEE DETECTED. INTERACTIVE MODE ENABLED.', 'system-text');
        addLine(output, '', 'system-text');

        // Build prompt buttons
        Object.keys(RESPONSES).forEach(prompt => {
            const btn = document.createElement('button');
            btn.className = 'terminal-prompt-btn';
            btn.textContent = prompt;
            btn.addEventListener('click', () => handlePrompt(prompt, output, promptsContainer));
            promptsContainer.appendChild(btn);
        });
    }

    function handlePrompt(prompt, output, promptsContainer) {
        // Disable buttons during response
        const buttons = promptsContainer.querySelectorAll('.terminal-prompt-btn');
        buttons.forEach(b => b.disabled = true);

        // Show user prompt
        addLine(output, '> ' + prompt, 'prompt-text');

        // Pick a random response
        const responses = RESPONSES[prompt];
        const response = responses[Math.floor(Math.random() * responses.length)];

        // Type out the response
        typeResponse(output, response, () => {
            buttons.forEach(b => b.disabled = false);
            output.scrollTop = output.scrollHeight;

            // Random chance of a glitch after responding
            if (Math.random() < 0.2) {
                setTimeout(() => {
                    const terminal = document.getElementById('slop-terminal');
                    if (terminal) {
                        terminal.style.opacity = '0.7';
                        setTimeout(() => terminal.style.opacity = '1', 150);
                        setTimeout(() => {
                            terminal.style.opacity = '0.85';
                            setTimeout(() => terminal.style.opacity = '1', 100);
                        }, 300);
                    }
                }, 500);
            }
        });
    }

    function typeResponse(output, text, callback) {
        const lineEl = document.createElement('div');
        lineEl.className = 'line response-text';
        output.appendChild(lineEl);

        const cursor = document.createElement('span');
        cursor.className = 'typing-cursor';

        let i = 0;
        const speed = 25 + Math.random() * 15;

        function tick() {
            if (i < text.length) {
                if (text[i] === '\n') {
                    lineEl.appendChild(document.createElement('br'));
                } else {
                    lineEl.appendChild(document.createTextNode(text[i]));
                }
                // Keep cursor at the end
                if (cursor.parentElement) cursor.remove();
                lineEl.appendChild(cursor);

                i++;
                output.scrollTop = output.scrollHeight;

                // Variable speed: pause longer on punctuation
                let delay = speed;
                if (text[i - 1] === '.' || text[i - 1] === '?') delay = speed * 4;
                else if (text[i - 1] === ',') delay = speed * 2;
                else if (text[i - 1] === '\u2588') delay = speed * 0.3;

                setTimeout(tick, delay);
            } else {
                if (cursor.parentElement) cursor.remove();
                addLine(output, '', 'system-text');
                if (callback) callback();
            }
        }

        tick();
    }

    function addLine(output, text, className) {
        const line = document.createElement('div');
        line.className = 'line ' + (className || '');
        line.textContent = text;
        output.appendChild(line);
        output.scrollTop = output.scrollHeight;
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
