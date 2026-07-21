import { formatDateTime, formatFileSize } from './utils.js';
import { getTagifyOptions, loadTagWhitelist } from './tagify-options.js';

class ImageDetailsViewModel {
    constructor(imageId) {
        this.imageId = imageId;
        this.image = ko.observable(null);
        this.loading = ko.observable(true);
        this.ocrText = ko.observable('');
        this.savedOcrText = ko.observable('');

        this.ocrTextDirty = ko.computed(() => this.ocrText() !== this.savedOcrText());

        this.formatDateTime = formatDateTime;
        this.formatFileSize = formatFileSize;

        this._tagify = null;
    }

    init = async () => {
        try {
            const response = await fetch(`/api/images/${this.imageId}`);
            if (!response.ok) {
                toast.error('Image not found.');
                return;
            }

            const data = await response.json();
            this.image(data);
            this.ocrText(data.ocrText || '');
            this.savedOcrText(data.ocrText || '');

            await this._initTags(data);
        } catch (error) {
            console.error('Error loading image:', error);
            toast.error('Failed to load image.');
        } finally {
            this.loading(false);
        }
    };

    _initTags = async (image) => {
        const whitelist = await loadTagWhitelist();
        this._tagify = new Tagify(document.getElementById('detailTagsInput'), getTagifyOptions(whitelist));

        let suppress = true;
        this._tagify.addTags(image.tags || []);
        suppress = false;

        this._tagify.on('change', async () => {
            if (suppress) return;

            const tagNames = this._tagify.value.map(t => t.value);
            const response = await fetch(`/api/images/${this.imageId}/tags`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ tagNames })
            });

            if (!response.ok) {
                toast.error('Failed to save tags.');
            }
        });
    };

    saveOcrText = async () => {
        try {
            const response = await fetch(`/api/images/${this.imageId}/ocr`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ ocrText: this.ocrText() })
            });

            if (response.ok) {
                this.savedOcrText(this.ocrText());
                toast.success('Text updated.');
            } else {
                toast.error('Failed to update text.');
            }
        } catch (error) {
            console.error('Error saving OCR text:', error);
            toast.error('Failed to update text.');
        }
    };

    deleteImage = async () => {
        const image = this.image();
        if (!image) return;

        if (!confirm(`Delete "${image.fileName}"? This cannot be undone.`)) return;

        try {
            const response = await fetch(`/api/images/${this.imageId}`, { method: 'DELETE' });

            if (response.ok) {
                window.location.href = '/';
            } else {
                const data = await response.json().catch(() => ({}));
                toast.error(data.message || 'Failed to delete image.');
            }
        } catch (error) {
            console.error('Error deleting image:', error);
            toast.error('Failed to delete image.');
        }
    };
}

var viewModel;

document.addEventListener('DOMContentLoaded', async () => {
    viewModel = new ImageDetailsViewModel(imageId);
    ko.applyBindings(viewModel);
    await viewModel.init();
});
