/**
 * Runs in <head> before paint so the stored theme applies without a flash.
 * Keep ALLOWED + DARK + DEFAULT in sync with Constants.BootswatchThemes in PicStashly.Core.
 */
(function () {
    var KEY = 'picstashly.theme';
    var DEFAULT = 'darkly';
    var ALLOWED = [
        'brite', 'cerulean', 'cosmo', 'cyborg', 'darkly', 'flatly', 'journal', 'litera', 'lumen',
        'lux', 'materia', 'minty', 'morph', 'pulse', 'quartz', 'sandstone', 'simplex', 'sketchy',
        'slate', 'solar', 'spacelab', 'superhero', 'united', 'vapor', 'yeti', 'zephyr'
    ];
    var DARK = { cyborg: 1, darkly: 1, slate: 1, solar: 1, superhero: 1, vapor: 1 };

    try {
        var raw = localStorage.getItem(KEY);
        var t = raw ? String(raw).toLowerCase() : DEFAULT;
        if (ALLOWED.indexOf(t) < 0) {
            t = DEFAULT;
        }

        var link = document.getElementById('theme-stylesheet');
        if (link) {
            link.setAttribute('href', '/lib/bootswatch/dist/' + t + '/bootstrap.min.css');
        }
        document.documentElement.setAttribute(
            'data-bs-theme',
            DARK[t] ? 'dark' : 'light');
        // Note: <nav> does not exist yet (this runs in <head>). theme-picker.js syncs
        // nav[data-bs-theme] on DOMContentLoaded; site.css also keys off html[data-bs-theme].
    } catch (e) {
        /* private mode / blocked storage */
    }
})();
