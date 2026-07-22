import { formatDateTime, formatFileSize, copyImageToClipboard } from './utils.js';
import { getTagifyOptions, loadTagWhitelist } from './tagify-options.js';
import { loadCategories } from './categories-shared.js';

class ImageDetailsViewModel {
    constructor(imageId) {
        this.imageId = imageId;
        this.image = ko.observable(null);
        this.loading = ko.observable(true);
        this.ocrText = ko.observable('');
        this.savedOcrText = ko.observable('');
        this.categoryId = ko.observable(null);
        this.categories = ko.observableArray([]);

        this.ocrTextDirty = ko.computed(() => this.ocrText() !== this.savedOcrText());

        this.formatDateTime = formatDateTime;
        this.formatFileSize = formatFileSize;

        this._tagify = null;
    }

    // Must run BEFORE ko.applyBindings — see the comment on the equivalent method in library.js.
    loadFilterData = async () => {
        this.categories(await loadCategories());
    };

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
            this.categoryId(data.categoryId ?? null);

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

    changeCategory = async () => {
        const categoryId = this.categoryId() ? Number(this.categoryId()) : null;

        try {
            const response = await fetch(`/api/images/${this.imageId}/category`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ categoryId })
            });

            const data = await response.json().catch(() => ({}));

            if (response.ok) {
                this.image(data);
                this.categoryId(data.categoryId ?? null);
                toast.success('Category updated.');
            } else {
                toast.error(data.message || 'Failed to update category.');
            }
        } catch (error) {
            console.error('Error updating category:', error);
            toast.error('Failed to update category.');
        }
    };

    copyImage = async () => {
        const image = this.image();
        if (!image) return;

        try {
            await copyImageToClipboard(image.url);
            toast.success('Image copied to clipboard.');
        } catch (error) {
            console.error('Error copying image:', error);
            toast.error(error.message || 'Failed to copy image.');
        }
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
    await viewModel.loadFilterData();
    ko.applyBindings(viewModel);
    await viewModel.init();
});
