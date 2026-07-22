/**
 * Bootswatch theme picker — preview on click, commit with OK, restore on Cancel/dismiss.
 * Keep ALLOWED + DARK + DEFAULT in sync with Constants.BootswatchThemes / theme-init.js.
 */
(function () {
    const KEY = 'picstashly.theme';
    const DEFAULT = 'darkly';
    const ALLOWED = [
        'brite', 'cerulean', 'cosmo', 'cyborg', 'darkly', 'flatly', 'journal', 'litera', 'lumen',
        'lux', 'materia', 'minty', 'morph', 'pulse', 'quartz', 'sandstone', 'simplex', 'sketchy',
        'slate', 'solar', 'spacelab', 'superhero', 'united', 'vapor', 'yeti', 'zephyr'
    ];
    const DARK = { cyborg: 1, darkly: 1, slate: 1, solar: 1, superhero: 1, vapor: 1 };

    let committedTheme = DEFAULT;
    let previewTheme = DEFAULT;
    let closedWithOk = false;

    function normalizeTheme(raw) {
        if (!raw) return DEFAULT;
        const t = String(raw).trim().toLowerCase();
        return ALLOWED.includes(t) ? t : DEFAULT;
    }

    function isDark(theme) {
        return !!DARK[theme];
    }

    function getStoredTheme() {
        try {
            return normalizeTheme(localStorage.getItem(KEY));
        } catch {
            return DEFAULT;
        }
    }

    function setStoredTheme(theme) {
        try {
            localStorage.setItem(KEY, theme);
        } catch {
            /* private mode / blocked storage */
        }
    }

    function applyThemeToDom(theme) {
        const t = normalizeTheme(theme);
        const scheme = isDark(t) ? 'dark' : 'light';
        const link = document.getElementById('theme-stylesheet');
        if (link) {
            link.setAttribute('href', `/lib/bootswatch/dist/${t}/bootstrap.min.css`);
        }
        document.documentElement.setAttribute('data-bs-theme', scheme);
        document.querySelectorAll('nav.navbar').forEach(nav => {
            nav.setAttribute('data-bs-theme', scheme);
        });
        previewTheme = t;
        updateActiveButtons();
        activateMatchingTab(t);
    }

    function updateActiveButtons() {
        document.querySelectorAll('[data-theme-name]').forEach(btn => {
            const name = btn.getAttribute('data-theme-name');
            btn.classList.toggle('active', name === previewTheme);

            let badge = btn.querySelector('.theme-saved-badge');
            if (name === committedTheme) {
                if (!badge) {
                    badge = document.createElement('span');
                    badge.className = 'badge rounded-pill theme-saved-badge';
                    badge.textContent = 'Saved';
                    btn.appendChild(badge);
                }
            } else if (badge) {
                badge.remove();
            }
        });
    }

    function activateMatchingTab(theme) {
        const tabId = isDark(theme) ? 'theme-tab-dark' : 'theme-tab-light';
        const tabBtn = document.getElementById(tabId);
        if (!tabBtn || typeof bootstrap === 'undefined') return;
        bootstrap.Tab.getOrCreateInstance(tabBtn).show();
    }

    function hideModal() {
        const el = document.getElementById('themePickerModal');
        if (!el || typeof bootstrap === 'undefined') return;
        bootstrap.Modal.getInstance(el)?.hide();
    }

    document.addEventListener('DOMContentLoaded', () => {
        committedTheme = getStoredTheme();
        previewTheme = committedTheme;
        // Re-apply so <nav data-bs-theme> matches (theme-init runs before <nav> exists).
        applyThemeToDom(committedTheme);

        document.querySelectorAll('[data-theme-name]').forEach(btn => {
            btn.addEventListener('click', () => {
                const theme = btn.getAttribute('data-theme-name');
                if (theme && theme !== previewTheme) {
                    applyThemeToDom(theme);
                }
            });
        });

        document.getElementById('themePickerOk')?.addEventListener('click', () => {
            setStoredTheme(previewTheme);
            committedTheme = previewTheme;
            closedWithOk = true;
            updateActiveButtons();
            hideModal();
        });

        document.getElementById('themePickerCancel')?.addEventListener('click', () => {
            applyThemeToDom(committedTheme);
            hideModal();
        });

        const modal = document.getElementById('themePickerModal');
        modal?.addEventListener('show.bs.modal', () => {
            previewTheme = committedTheme;
            updateActiveButtons();
            activateMatchingTab(committedTheme);
        });

        modal?.addEventListener('hidden.bs.modal', () => {
            if (closedWithOk) {
                closedWithOk = false;
                return;
            }
            if (previewTheme !== committedTheme) {
                applyThemeToDom(committedTheme);
            }
        });
    });
})();
