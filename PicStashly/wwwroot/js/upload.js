import { getTagifyOptions, loadTagWhitelist } from './tagify-options.js';
import { loadCategories } from './categories-shared.js';

let nextQueueId = 1;

class UploadQueueItem {
    constructor(file) {
        this.queueId = nextQueueId++;
        this.file = file;
        this.fileName = file.name || 'pasted-image.png';
        this.previewUrl = URL.createObjectURL(file);
        this.status = ko.observable('pending'); // pending | uploading | saved | duplicate | error
        this.errorMessage = ko.observable('');
        this.duplicates = ko.observableArray([]);
        this.savedImage = null;
        this._tagify = null;
    }
}

class UploadViewModel {
    constructor() {
        this.queue = ko.observableArray([]);
        this.categories = ko.observableArray([]);
        this.selectedCategoryId = ko.observable('');
        // Freshly-uploaded images never have tags yet, so "merge" only ever means: apply the
        // batch tags typed above onto the existing (duplicate) image instead of this new one.
        this.hasBatchTags = ko.observable(false);
        this._batchTagify = null;
        this._tagWhitelist = [];
        this._processing = false;
    }

    // Must run BEFORE ko.applyBindings — see the comment on the equivalent method in library.js.
    loadFilterData = async () => {
        this._tagWhitelist = await loadTagWhitelist();
        this.categories(await loadCategories());
    };

    init = async () => {
        this._batchTagify = new Tagify(
            document.getElementById('batchTagsInput'),
            getTagifyOptions(this._tagWhitelist));
        this._batchTagify.on('change', () => this.hasBatchTags(this._batchTagify.value.length > 0));

        const dropZone = document.getElementById('dropZone');
        const fileInput = document.getElementById('fileInput');

        dropZone.addEventListener('click', () => fileInput.click());
        fileInput.addEventListener('change', () => {
            this.addFiles([...fileInput.files]);
            fileInput.value = '';
        });

        ['dragenter', 'dragover'].forEach(eventName =>
            dropZone.addEventListener(eventName, (e) => {
                e.preventDefault();
                dropZone.classList.add('drag-over');
            }));

        ['dragleave', 'drop'].forEach(eventName =>
            dropZone.addEventListener(eventName, (e) => {
                e.preventDefault();
                dropZone.classList.remove('drag-over');
            }));

        // Whole page accepts drops and pastes, per the "capture friction" principle.
        document.addEventListener('dragover', (e) => e.preventDefault());
        document.addEventListener('drop', (e) => {
            e.preventDefault();
            const files = [...(e.dataTransfer?.files || [])].filter(f => f.type.startsWith('image/'));
            if (files.length > 0) this.addFiles(files);
        });

        document.addEventListener('paste', (e) => {
            const files = [...(e.clipboardData?.items || [])]
                .filter(i => i.kind === 'file' && i.type.startsWith('image/'))
                .map(i => i.getAsFile());
            if (files.length > 0) {
                e.preventDefault();
                this.addFiles(files);
            }
        });
    };

    addFiles = (files) => {
        for (const file of files) {
            this.queue.push(new UploadQueueItem(file));
        }
        this._processQueue();
    };

    _processQueue = async () => {
        if (this._processing) return;
        this._processing = true;

        try {
            let item;
            while ((item = this.queue().find(i => i.status() === 'pending'))) {
                await this._uploadItem(item, false);
            }
        } finally {
            this._processing = false;
        }
    };

    _uploadItem = async (item, force) => {
        item.status('uploading');

        try {
            const formData = new FormData();
            formData.append('file', item.file, item.fileName);
            formData.append('force', force);

            const batchTags = this._batchTagify.value.map(t => t.value);
            if (batchTags.length > 0) {
                formData.append('tags', batchTags.join(','));
            }
            if (this.selectedCategoryId()) {
                formData.append('categoryId', this.selectedCategoryId());
            }

            const response = await fetch('/api/images/upload', { method: 'POST', body: formData });
            const data = await response.json().catch(() => ({}));

            if (!response.ok) {
                item.errorMessage(data.message || 'Upload failed.');
                item.status('error');
                return;
            }

            if (data.isDuplicate) {
                item.duplicates(data.duplicates || []);
                item.status('duplicate');
                return;
            }

            item.savedImage = data.image;
            item.status('saved');
            setTimeout(() => this._initPerImageTags(item), 0);
        } catch (error) {
            console.error('Upload error:', error);
            item.errorMessage('Upload failed.');
            item.status('error');
        }
    };

    _initPerImageTags = (item) => {
        if (item._tagify || !item.savedImage) return;

        const input = document.querySelector(`.per-image-tags[data-queue-id="${item.queueId}"]`);
        if (!input) return;

        item._tagify = new Tagify(input, getTagifyOptions(this._tagWhitelist));
        item._tagify.addTags(item.savedImage.tags || []);

        let suppress = false;
        item._tagify.on('change', async () => {
            if (suppress) return;
            suppress = true;

            try {
                const tagNames = item._tagify.value.map(t => t.value);
                const response = await fetch(`/api/images/${item.savedImage.id}/tags`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ tagNames })
                });

                if (!response.ok) {
                    toast.error('Failed to save tags.');
                }
            } finally {
                suppress = false;
            }
        });
    };

    saveAnyway = (item) => this._uploadItem(item, true);

    // New uploads never have tags yet — this applies the batch tags typed above onto the
    // existing (duplicate) image instead, then discards this upload. Disabled when the
    // batch tags box is empty, since there'd be nothing to merge.
    mergeTags = async (item) => {
        const batchTags = this._batchTagify.value.map(t => t.value);
        const target = item.duplicates()[0];

        if (!target || batchTags.length === 0) {
            this.discard(item);
            return;
        }

        try {
            const response = await fetch('/api/images/bulk-tags', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    imageIds: [target.id],
                    tagNames: batchTags,
                    remove: false
                })
            });

            if (!response.ok) {
                toast.error('Failed to merge tags.');
                return;
            }

            toast.success(`Tags merged into "${target.fileName}".`);
        } catch (error) {
            console.error('Error merging tags:', error);
            toast.error('Failed to merge tags.');
            return;
        }

        this.discard(item);
    };

    discard = (item) => {
        URL.revokeObjectURL(item.previewUrl);
        this.queue.remove(item);
    };

    clearFinished = () => {
        this.queue().filter(i => i.status() === 'saved' || i.status() === 'error')
            .forEach(i => URL.revokeObjectURL(i.previewUrl));
        this.queue.remove(i => i.status() === 'saved' || i.status() === 'error');
    };
}

var viewModel;

document.addEventListener('DOMContentLoaded', async () => {
    viewModel = new UploadViewModel();
    await viewModel.loadFilterData();
    ko.applyBindings(viewModel);
    await viewModel.init();
});
