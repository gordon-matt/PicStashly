import { getTagifyOptions, loadTagWhitelist } from './tagify-options.js';

class CaptureViewModel {
    constructor(imageUrl, pageUrl) {
        this.imageUrl = ko.observable(imageUrl);
        this.pageUrl = pageUrl;
        this.status = ko.observable('idle'); // idle | saving | saved | duplicate | error
        this.savedId = ko.observable(null);
        this.duplicates = ko.observableArray([]);
        this.errorMessage = ko.observable('');
        this.ocrText = ko.observable('');
        this.savedOcrText = ko.observable('');
        this.ocrTextDirty = ko.computed(() => this.ocrText() !== this.savedOcrText());

        this._tagify = null;
    }

    init = async () => {
        const whitelist = await loadTagWhitelist();
        this._tagify = new Tagify(document.getElementById('captureTagsInput'), getTagifyOptions(whitelist));
    };

    save = () => this._capture(false);

    saveAnyway = () => this._capture(true);

    _capture = async (force) => {
        this.status('saving');

        try {
            const response = await fetch(`/api/capture?force=${force}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    imageUrl: this.imageUrl(),
                    pageUrl: this.pageUrl,
                    tagNames: this._tagify.value.map(t => t.value)
                })
            });

            const data = await response.json().catch(() => ({}));

            if (!response.ok) {
                this.errorMessage(data.message || 'Capture failed.');
                this.status('error');
                return;
            }

            if (data.isDuplicate) {
                this.duplicates(data.duplicates || []);
                this.status('duplicate');
            } else {
                this.savedId(data.image.id);
                this.ocrText(data.image.ocrText || '');
                this.savedOcrText(data.image.ocrText || '');
                this.status('saved');
            }
        } catch (error) {
            console.error('Capture failed:', error);
            this.errorMessage('Capture failed.');
            this.status('error');
        }
    };

    saveOcrText = async () => {
        const id = this.savedId();
        if (!id) return;

        try {
            const response = await fetch(`/api/images/${id}/ocr`, {
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
}

var viewModel;

document.addEventListener('DOMContentLoaded', async () => {
    viewModel = new CaptureViewModel(captureImageUrl, capturePageUrl);
    ko.applyBindings(viewModel);
    await viewModel.init();
});
