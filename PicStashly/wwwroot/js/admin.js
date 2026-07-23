import { formatFileSize, formatNumber } from './utils.js';

class AdminViewModel {
    constructor() {
        this.stats = ko.observable(null);
        this.busy = ko.observable(false);
        this.maintenanceMessage = ko.observable('');
        this.orphanResult = ko.observable(null);

        this.users = ko.observableArray([]);
        this.usersLoading = ko.observable(true);

        this.formatFileSize = formatFileSize;
        this.formatNumber = formatNumber;
    }

    init = async () => {
        this._buildBookmarklet();
        await this.loadStats();

        if (window.picstashlyIsIdentityAuth) {
            await this.loadUsers();
        }
    };

    loadUsers = async () => {
        this.usersLoading(true);
        try {
            const response = await fetch('/api/admin/users');
            if (response.ok) {
                const data = await response.json();
                const currentUserId = window.picstashlyCurrentUserId;
                this.users(data.map(u => ({
                    ...u,
                    role: ko.observable(u.role),
                    isCurrentUser: u.userId === currentUserId
                })));
            }
        } catch (error) {
            console.error('Error loading users:', error);
        } finally {
            this.usersLoading(false);
        }
    };

    approveUser = async (user) => {
        try {
            const response = await fetch(`/api/admin/users/${user.userId}/approve`, { method: 'POST' });
            if (response.ok) {
                toast.success(`${user.email} approved.`);
                user.isPendingApproval = false;
                this.users.valueHasMutated();
            } else {
                const err = await response.json().catch(() => null);
                toast.error(err?.message || 'Could not approve user.');
            }
        } catch (error) {
            console.error('Error approving user:', error);
            toast.error('Error approving user.');
        }
    };

    lockUser = async (user) => {
        if (!confirm(`Lock account for ${user.email}?`)) return;
        try {
            const response = await fetch(`/api/admin/users/${user.userId}/lock`, { method: 'POST' });
            if (response.ok) {
                toast.success(`${user.email} locked.`);
                user.isPendingApproval = true;
                this.users.valueHasMutated();
            } else {
                const err = await response.json().catch(() => null);
                toast.error(err?.message || 'Could not lock user.');
            }
        } catch (error) {
            console.error('Error locking user:', error);
            toast.error('Error locking user.');
        }
    };

    changeUserRole = async (user, evt) => {
        // Read from the DOM: Knockout's `change` can run before `value` updates the observable,
        // so user.role() would still be the previous selection (often wrong).
        const newRole = evt?.target?.value ?? user.role();
        try {
            const response = await fetch(`/api/admin/users/${user.userId}/role`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ role: newRole })
            });
            if (response.ok) {
                user.role(newRole);
                toast.success(`${user.email} is now ${newRole}.`);
            } else {
                const err = await response.json().catch(() => null);
                toast.error(err?.message || 'Could not update role.');
                await this.loadUsers();
            }
        } catch (error) {
            console.error('Error updating role:', error);
            toast.error('Error updating role.');
            await this.loadUsers();
        }
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
        // IMPORTANT: never use "#" inside the bookmarklet source (e.g. in CSS colors). Browsers
        // treat "#" as starting the URL fragment and will truncate the javascript: href, which
        // silently breaks the whole bookmarklet. Use rgb()/rgba() instead.
        //
        // Many sites lazy-load or serve responsive images, where <img src>/currentSrc points at
        // a tiny placeholder or a low-res srcset candidate rather than the real picture. That
        // still looks fine on the page (the browser scales it up) but is often too small/blurry
        // for OCR once captured. So this walks the common lazy-load data-* attributes and the
        // largest srcset candidate first, only falling back to src/currentSrc, and skips any
        // "data:" placeholder URLs it finds along the way. It also supports CSS background-image
        // elements for sites that don't use a plain <img>.
        const source = `(function(){
            var origin = '${origin}';
            var prevCursor = document.body.style.cursor;
            var lastEl = null;
            var overlay = null;
            var toast = null;

            function showToast(msg, ms) {
                if (toast) toast.remove();
                toast = document.createElement('div');
                toast.textContent = msg;
                toast.style.cssText = 'position:fixed;top:20px;left:50%;transform:translateX(-50%);' +
                    'background:rgb(220,38,38);color:rgb(255,255,255);padding:14px 22px;border-radius:8px;' +
                    'font:bold 16px/1.4 system-ui,sans-serif;z-index:2147483647;pointer-events:none;' +
                    'box-shadow:0 6px 24px rgba(0,0,0,0.45);border:2px solid rgb(255,255,255)';
                document.documentElement.appendChild(toast);
                setTimeout(function () {
                    if (toast) { toast.remove(); toast = null; }
                }, ms || 3000);
            }

            if (!document.images.length) {
                showToast('No images found on this page.');
                return;
            }

            overlay = document.createElement('div');
            overlay.style.cssText = 'position:fixed;display:none;pointer-events:none;z-index:2147483646;' +
                'box-sizing:border-box;border:3px solid rgb(59,130,246);border-radius:4px;' +
                'box-shadow:0 0 0 2px rgba(59,130,246,0.4)';
            document.documentElement.appendChild(overlay);
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

            function findPickable(x, y) {
                var els = document.elementsFromPoint(x, y);
                for (var i = 0; i < els.length; i++) {
                    var el = els[i];
                    if (el === overlay || el === toast) continue;
                    if (el.tagName === 'IMG') return el;
                    if (el.tagName === 'PICTURE') {
                        var img = el.querySelector('img');
                        if (img) return img;
                    }
                    if (resolveBackgroundUrl(el)) return el;
                }
                return null;
            }

            function clearHighlight() {
                lastEl = null;
                if (overlay) overlay.style.display = 'none';
            }

            function updateOverlay(el) {
                var r = el.getBoundingClientRect();
                if (r.width < 2 || r.height < 2) { clearHighlight(); return; }
                overlay.style.display = 'block';
                overlay.style.top = r.top + 'px';
                overlay.style.left = r.left + 'px';
                overlay.style.width = r.width + 'px';
                overlay.style.height = r.height + 'px';
            }

            function onMove(e) {
                var el = findPickable(e.clientX, e.clientY);
                if (el === lastEl) {
                    if (el) updateOverlay(el);
                    return;
                }
                lastEl = el;
                if (el) updateOverlay(el);
                else clearHighlight();
            }

            function onScroll() {
                if (lastEl) updateOverlay(lastEl);
            }

            function cleanup() {
                document.removeEventListener('click', onClick, true);
                document.removeEventListener('mousemove', onMove, true);
                document.removeEventListener('keydown', onKey, true);
                document.removeEventListener('scroll', onScroll, true);
                clearHighlight();
                document.body.style.cursor = prevCursor;
                if (overlay) { overlay.remove(); overlay = null; }
            }

            function onKey(e) {
                if (e.key === 'Escape') {
                    e.preventDefault();
                    cleanup();
                    showToast('Cancelled.', 1200);
                }
            }

            function onClick(e) {
                var target = findPickable(e.clientX, e.clientY);
                if (!target) return;
                var url = target.tagName === 'IMG' ? resolveImageUrl(target) : resolveBackgroundUrl(target);
                if (!url) return;
                e.preventDefault();
                e.stopPropagation();
                cleanup();
                window.open(
                    origin + '/capture?imageUrl=' + encodeURIComponent(new URL(url, location.href).href) +
                    '&pageUrl=' + encodeURIComponent(location.href),
                    '_blank');
            }

            document.addEventListener('click', onClick, true);
            document.addEventListener('mousemove', onMove, true);
            document.addEventListener('keydown', onKey, true);
            document.addEventListener('scroll', onScroll, true);
            showToast('Click an image to stash it. Esc to cancel.');
        })();`;

        document.getElementById('bookmarkletLink')
            .setAttribute('href', 'javascript:' + source.replace(/\s+/g, ' ').trim());
    };
}

var viewModel;

document.addEventListener('DOMContentLoaded', async () => {
    viewModel = new AdminViewModel();
    ko.applyBindings(viewModel);
    await viewModel.init();
});
