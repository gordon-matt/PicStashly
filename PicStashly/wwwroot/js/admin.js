import { formatFileSize, formatNumber } from './utils.js';

class AdminViewModel {
    constructor() {
        this.stats = ko.observable(null);
        this.busy = ko.observable(false);
        this.maintenanceMessage = ko.observable('');
        this.orphanResult = ko.observable(null);

        this.formatFileSize = formatFileSize;
        this.formatNumber = formatNumber;
    }

    init = async () => {
        this._buildBookmarklet();
        await this.loadStats();
    };

    loadStats = async () => {
        try {
            const response = await fetch('/api/admin/stats');
            if (response.ok) {
                this.stats(await response.json());
            }
        } catch (error) {
            console.error('Error loading stats:', error);
        }
    };

    runOrphanCheck = async () => {
        this.busy(true);
        this.maintenanceMessage('');
        this.orphanResult(null);

        try {
            const response = await fetch('/api/admin/orphan-check', { method: 'POST' });
            const data = await response.json();

            if (response.ok) {
                this.orphanResult(data);
                if (data.orphanedFiles.length === 0 && data.brokenImages.length === 0) {
                    this.maintenanceMessage('All good — no orphaned files or broken images found.');
                }
            } else {
                toast.error(data.message || 'Orphan check failed.');
            }
        } catch (error) {
            console.error('Orphan check failed:', error);
            toast.error('Orphan check failed.');
        } finally {
            this.busy(false);
        }
    };

    runRehash = () => this._runMaintenance('/api/admin/rehash', 'Re-hash');

    runReOcr = () => {
        if (!confirm('Re-running OCR overwrites the stored text for every image, including manual corrections. Continue?')) return;
        this._runMaintenance('/api/admin/reocr', 'OCR');
    };

    _runMaintenance = async (url, label) => {
        this.busy(true);
        this.maintenanceMessage('');

        try {
            const response = await fetch(url, { method: 'POST' });
            const data = await response.json();

            if (response.ok) {
                this.maintenanceMessage(`${label} finished: ${data.processed} processed, ${data.failed} failed.`);
            } else {
                toast.error(data.message || `${label} failed.`);
            }
        } catch (error) {
            console.error(`${label} failed:`, error);
            toast.error(`${label} failed.`);
        } finally {
            this.busy(false);
        }
    };

    _buildBookmarklet = () => {
        const origin = window.location.origin;

        // Written readably here, then minified to one line before it's used as the href —
        // real bookmarklets get stored/copied as raw text, so newlines aren't safe to keep.
        //
        // Many sites lazy-load or serve responsive images, where <img src>/currentSrc points at
        // a tiny placeholder or a low-res srcset candidate rather than the real picture. That
        // still looks fine on the page (the browser scales it up) but is often too small/blurry
        // for OCR once captured. So this walks the common lazy-load data-* attributes and the
        // largest srcset candidate first, only falling back to src/currentSrc, and skips any
        // "data:" placeholder URLs it finds along the way. It also supports CSS background-image
        // elements for sites that don't use a plain <img>.
        const source = `(function(){
            if (document.images.length === 0) {
                alert('No images found on this page.');
                return;
            }

            var origin = '${origin}';
            var prev = document.body.style.cursor;
            document.body.style.cursor = 'crosshair';

            function pickLargestFromSrcset(srcset) {
                var best = null, bestScore = -1;
                srcset.split(',').forEach(function (candidate) {
                    var parts = candidate.trim().split(/\\s+/);
                    var url = parts[0];
                    var score = 0;
                    if (parts[1]) {
                        var wMatch = parts[1].match(/^(\\d+(?:\\.\\d+)?)w$/);
                        var xMatch = parts[1].match(/^(\\d+(?:\\.\\d+)?)x$/);
                        if (wMatch) score = parseFloat(wMatch[1]);
                        else if (xMatch) score = parseFloat(xMatch[1]) * 1000;
                    }
                    if (url && score >= bestScore) { best = url; bestScore = score; }
                });
                return best;
            }

            function isPlaceholder(url) {
                return !url || url.indexOf('data:') === 0;
            }

            function resolveImageUrl(el) {
                var lazyAttrs = ['data-src', 'data-original', 'data-lazy-src', 'data-full-src', 'data-large-src', 'data-zoom-src'];
                for (var i = 0; i < lazyAttrs.length; i++) {
                    var v = el.getAttribute(lazyAttrs[i]);
                    if (!isPlaceholder(v)) return v;
                }

                var srcsetAttrs = ['data-srcset', 'srcset'];
                for (var j = 0; j < srcsetAttrs.length; j++) {
                    var ss = el.getAttribute(srcsetAttrs[j]);
                    if (ss) {
                        var best = pickLargestFromSrcset(ss);
                        if (!isPlaceholder(best)) return best;
                    }
                }

                if (!isPlaceholder(el.currentSrc)) return el.currentSrc;
                if (!isPlaceholder(el.src)) return el.src;
                return el.currentSrc || el.src || null;
            }

            function resolveBackgroundUrl(el) {
                var bg = getComputedStyle(el).backgroundImage;
                var match = bg && bg.match(/url\\(["']?(.*?)["']?\\)/);
                return match ? match[1] : null;
            }

            function handleClick(e) {
                var target = e.target;
                var url = target.tagName === 'IMG' ? resolveImageUrl(target) : resolveBackgroundUrl(target);
                if (!url) return;

                e.preventDefault();
                e.stopPropagation();
                document.removeEventListener('click', handleClick, true);
                document.body.style.cursor = prev;

                var absoluteUrl = new URL(url, location.href).href;
                window.open(
                    origin + '/capture?imageUrl=' + encodeURIComponent(absoluteUrl) + '&pageUrl=' + encodeURIComponent(location.href),
                    '_blank');
            }

            document.addEventListener('click', handleClick, true);
            alert('Click an image on the page to stash it.');
        })();`;

        const minified = source.replace(/\s+/g, ' ').trim();
        document.getElementById('bookmarkletLink').setAttribute('href', 'javascript:' + minified);
    };
}

var viewModel;

document.addEventListener('DOMContentLoaded', async () => {
    viewModel = new AdminViewModel();
    ko.applyBindings(viewModel);
    await viewModel.init();
});
