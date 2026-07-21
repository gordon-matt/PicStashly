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
        const code =
            "(function(){" +
            "if(document.images.length===0){alert('No images found on this page.');return;}" +
            "var prev=document.body.style.cursor;document.body.style.cursor='crosshair';" +
            "function h(e){var t=e.target;if(t.tagName==='IMG'){e.preventDefault();e.stopPropagation();" +
            "document.removeEventListener('click',h,true);document.body.style.cursor=prev;" +
            "window.open('" + origin + "/capture?imageUrl='+encodeURIComponent(t.currentSrc||t.src)+'&pageUrl='+encodeURIComponent(location.href),'_blank');}}" +
            "document.addEventListener('click',h,true);" +
            "alert('Click an image on the page to stash it.');" +
            "})();";

        document.getElementById('bookmarkletLink').setAttribute('href', 'javascript:' + code);
    };
}

var viewModel;

document.addEventListener('DOMContentLoaded', async () => {
    viewModel = new AdminViewModel();
    ko.applyBindings(viewModel);
    await viewModel.init();
});
