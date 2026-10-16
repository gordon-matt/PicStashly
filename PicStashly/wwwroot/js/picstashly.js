// Browser-only helpers for the Blazor pages: clipboard images, drag/paste into an
// InputFile, infinite-scroll, and the bookmarklet href. None of this is loaded by Identity.
window.picstashly = {
    copyImage: async function (url) {
        if (!navigator.clipboard || typeof ClipboardItem === 'undefined') {
            throw new Error('Copying images is not supported in this browser.');
        }

        const response = await fetch(url);
        if (!response.ok) {
            throw new Error('Failed to load image.');
        }

        const pngBlob = await blobToPngBlob(await response.blob());
        await navigator.clipboard.write([new ClipboardItem({ 'image/png': pngBlob })]);
    },

    bindDropZone: function (key, zone, input) {
        zone = resolveElement(zone);
        input = resolveElement(input);
        if (!zone || !input) {
            return;
        }

        const onOver = (event) => {
            event.preventDefault();
            zone.classList.add('drag-over');
        };
        const onLeave = (event) => {
            event.preventDefault();
            zone.classList.remove('drag-over');
        };
        const onDrop = (event) => {
            event.preventDefault();
            event.stopPropagation();
            zone.classList.remove('drag-over');
            assignFiles(input, event.dataTransfer && event.dataTransfer.files);
        };

        zone.addEventListener('dragenter', onOver);
        zone.addEventListener('dragover', onOver);
        zone.addEventListener('dragleave', onLeave);
        zone.addEventListener('drop', onDrop);
        remember(key, [
            function () { zone.removeEventListener('dragenter', onOver); },
            function () { zone.removeEventListener('dragover', onOver); },
            function () { zone.removeEventListener('dragleave', onLeave); },
            function () { zone.removeEventListener('drop', onDrop); }
        ]);
    },

    bindPageDrop: function (key, input) {
        input = resolveElement(input);
        if (!input) {
            return;
        }

        const onDragOver = (event) => event.preventDefault();
        const onDrop = (event) => {
            event.preventDefault();
            assignFiles(input, event.dataTransfer && event.dataTransfer.files);
        };
        const onPaste = (event) => {
            const files = [...(event.clipboardData && event.clipboardData.items || [])]
                .filter(item => item.kind === 'file' && item.type.startsWith('image/'))
                .map(item => item.getAsFile())
                .filter(Boolean);
            if (!files.length) {
                return;
            }

            event.preventDefault();
            assignFiles(input, files);
        };

        document.addEventListener('dragover', onDragOver);
        document.addEventListener('drop', onDrop);
        document.addEventListener('paste', onPaste);
        remember(key, [
            function () { document.removeEventListener('dragover', onDragOver); },
            function () { document.removeEventListener('drop', onDrop); },
            function () { document.removeEventListener('paste', onPaste); }
        ]);
    },

    unbind: function (key) {
        const cleanup = bindings.get(key);
        if (cleanup) {
            cleanup();
            bindings.delete(key);
        }
    },

    watchSentinel: function (element, dotnet) {
        if (!element) {
            return;
        }

        const existing = sentinels.get(element);
        if (existing) {
            existing.disconnect();
        }

        const observer = new IntersectionObserver(function (entries) {
            if (entries.some(entry => entry.isIntersecting)) {
                dotnet.invokeMethodAsync('LoadMoreFromScroll');
            }
        }, { rootMargin: '240px' });
        observer.observe(element);
        sentinels.set(element, observer);
    },

    focus: function (element) {
        if (element) {
            element.focus();
        }
    },

    // Must not contain "#": browsers truncate a javascript: href at the first hash.
    buildBookmarklet: function () {
        const origin = window.location.origin;
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

        return 'javascript:' + source.replace(/\s+/g, ' ').trim();
    }
};

const bindings = new Map();
const sentinels = new WeakMap();

function remember(key, cleanups) {
    const previous = bindings.get(key);
    if (previous) {
        previous();
    }

    bindings.set(key, function () {
        cleanups.forEach(function (cleanup) { cleanup(); });
    });
}

function resolveElement(value) {
    return typeof value === 'string' ? document.getElementById(value) : value;
}

function assignFiles(input, files) {
    const images = [...(files || [])].filter(file => file && file.type && file.type.startsWith('image/'));
    if (!images.length) {
        return;
    }

    const transfer = new DataTransfer();
    images.forEach(file => transfer.items.add(file));
    input.files = transfer.files;
    input.dispatchEvent(new Event('change', { bubbles: true }));
}

function blobToPngBlob(sourceBlob) {
    return new Promise((resolve, reject) => {
        const objectUrl = URL.createObjectURL(sourceBlob);
        const img = new Image();
        img.onload = () => {
            const canvas = document.createElement('canvas');
            canvas.width = img.naturalWidth;
            canvas.height = img.naturalHeight;
            canvas.getContext('2d').drawImage(img, 0, 0);
            canvas.toBlob((pngBlob) => {
                URL.revokeObjectURL(objectUrl);
                if (pngBlob) {
                    resolve(pngBlob);
                } else {
                    reject(new Error('Failed to encode image.'));
                }
            }, 'image/png');
        };
        img.onerror = () => {
            URL.revokeObjectURL(objectUrl);
            reject(new Error('Failed to decode image.'));
        };
        img.src = objectUrl;
    });
}
