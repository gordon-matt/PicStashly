import { getTagifyOptions, loadTagWhitelist } from './tagify-options.js';
import { loadCategories } from './categories-shared.js';

const PAGE_SIZE = 60;

class LibraryViewModel {
    constructor() {
        this.images = ko.observableArray([]);
        this.loading = ko.observable(false);
        this.hasMore = ko.observable(true);
        this.totalCount = ko.observable(0);

        // Filters
        this.searchQuery = ko.observable('');
        this.tagFilter = ko.observable('');
        this.categoryFilter = ko.observable('');
        this.categories = ko.observableArray([]);
        this.untaggedOnly = ko.observable(false);
        this.sort = ko.observable('newest');
        this.shuffleSeed = Math.floor(Math.random() * 2147483647);

        // Paging (infinite scroll)
        this.currentPage = 1;

        // Bulk selection
        this.selectionMode = ko.observable(false);
        this.selectedIds = ko.observableArray([]);
        this.canBulkTag = ko.computed(() =>
            this.selectedIds().length > 0 && this._bulkTagify && this._bulkTagify.value.length > 0);

        // Lightbox
        this.lightboxImage = ko.observable(null);

        this.hasActiveFilters = ko.computed(() =>
            !!this.searchQuery() || !!this.tagFilter() || !!this.categoryFilter() || this.untaggedOnly());

        // Debounced search
        let searchTimeout = null;
        this.searchQuery.subscribe(() => {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => this.reload(), 350);
        });

        this.untaggedOnly.subscribe(() => this.reload());
        this.categoryFilter.subscribe(() => this.reload());
        this.sort.subscribe(() => {
            this.shuffleSeed = Math.floor(Math.random() * 2147483647);
            this.reload();
        });

        this._tagFilterTagify = null;
        this._bulkTagify = null;
        this._lightboxTagify = null;
        this._suppressLightboxTagEvents = false;
        this._tagWhitelist = [];
    }

    // Loads data the initial bindings depend on (category dropdown options, tag whitelist).
    // Must run BEFORE ko.applyBindings: populating the "categories" observableArray afterwards
    // would rebuild the category <select>'s options while it's already bound, and Knockout can
    // spuriously write a value back onto "categoryFilter" when it does — which, combined with the
    // .subscribe(reload) below, triggered a second, overlapping "page 1" load on every page open.
    loadFilterData = async () => {
        this._tagWhitelist = await loadTagWhitelist();
        this.categories(await loadCategories());
    };

    init = async () => {
        this._tagFilterTagify = new Tagify(
            document.getElementById('tagFilterInput'),
            getTagifyOptions(this._tagWhitelist));
        this._tagFilterTagify.on('change', () => {
            this.tagFilter(this._tagFilterTagify.value.map(t => t.value).join(','));
            this.reload();
        });

        this._bulkTagify = new Tagify(
            document.getElementById('bulkTagsInput'),
            getTagifyOptions(this._tagWhitelist));
        this._bulkTagify.on('change', () => this.selectedIds.valueHasMutated());

        this._initInfiniteScroll();
        this._initKeyboard();
        this._initDropAndPaste();

        await this.loadPage();
    };

    reload = () => {
        this.currentPage = 1;
        this.images([]);
        this.hasMore(true);
        this.loadPage();
    };

    loadPage = async () => {
        if (this.loading()) return;
        this.loading(true);

        try {
            const params = new URLSearchParams({
                page: this.currentPage,
                pageSize: PAGE_SIZE,
                sort: this.sort()
            });
            if (this.searchQuery()) params.set('search', this.searchQuery());
            if (this.tagFilter()) params.set('tags', this.tagFilter());
            if (this.categoryFilter()) params.set('categoryId', this.categoryFilter());
            if (this.untaggedOnly()) params.set('untaggedOnly', 'true');
            if (this.sort() === 'random') params.set('shuffleSeed', this.shuffleSeed);

            const response = await fetch(`/api/images/search?${params}`);
            const data = await response.json();

            // Defensive de-dup: guards against ever double-appending the same page again
            // (e.g. a stray overlapping reload), regardless of what triggered it.
            const existingIds = new Set(this.images().map(i => i.id));
            const items = (data.images || []).filter(i => !existingIds.has(i.id));
            ko.utils.arrayPushAll(this.images, items);
            this.totalCount(data.totalCount ?? 0);
            this.hasMore(this.currentPage < (data.totalPages ?? 0));
        } catch (error) {
            console.error('Error loading images:', error);
            toast.error('Failed to load images.');
        } finally {
            this.loading(false);
        }
    };

    _initInfiniteScroll = () => {
        const sentinel = document.getElementById('scrollSentinel');
        const observer = new IntersectionObserver(async (entries) => {
            if (entries.some(e => e.isIntersecting) && this.hasMore() && !this.loading()) {
                this.currentPage++;
                await this.loadPage();
            }
        }, { rootMargin: '600px' });
        observer.observe(sentinel);
    };

    clearFilters = () => {
        this.searchQuery('');
        this.tagFilter('');
        this.categoryFilter('');
        this.untaggedOnly(false);
        if (this._tagFilterTagify) this._tagFilterTagify.removeAllTags();
        this.reload();
    };

    // ── Bulk selection ────────────────────────────────────────────────────

    toggleSelectionMode = () => {
        this.selectionMode(!this.selectionMode());
        if (!this.selectionMode()) {
            this.clearSelection();
        }
    };

    isSelected = (image) => this.selectedIds().includes(image.id);

    toggleSelected = (image) => {
        if (this.isSelected(image)) {
            this.selectedIds.remove(image.id);
        } else {
            this.selectedIds.push(image.id);
        }
    };

    selectAllVisible = () => {
        this.selectedIds(this.images().map(i => i.id));
    };

    clearSelection = () => this.selectedIds([]);

    applyBulkTags = () => this._bulkTag(false);

    removeBulkTags = () => this._bulkTag(true);

    _bulkTag = async (remove) => {
        const tagNames = this._bulkTagify.value.map(t => t.value);
        if (tagNames.length === 0 || this.selectedIds().length === 0) return;

        try {
            const response = await fetch('/api/images/bulk-tags', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    imageIds: this.selectedIds(),
                    tagNames: tagNames,
                    remove: remove
                })
            });

            if (!response.ok) {
                const data = await response.json().catch(() => ({}));
                toast.error(data.message || 'Failed to update tags.');
                return;
            }

            toast.success(remove ? 'Tags removed.' : 'Tags applied.');
            this._refreshTagsForImages(this.selectedIds());
        } catch (error) {
            console.error('Error bulk-tagging:', error);
            toast.error('Failed to update tags.');
        }
    };

    _refreshTagsForImages = async (imageIds) => {
        for (const id of imageIds) {
            try {
                const response = await fetch(`/api/images/${id}`);
                if (!response.ok) continue;
                const detail = await response.json();
                const image = this.images().find(i => i.id === id);
                if (image) {
                    image.tags = detail.tags;
                }
            } catch { /* non-critical refresh */ }
        }
        this.images.valueHasMutated();
    };

    // ── Item click / lightbox ─────────────────────────────────────────────

    onItemClick = (image) => {
        if (this.selectionMode()) {
            this.toggleSelected(image);
        } else {
            this.openLightbox(image);
        }
    };

    openLightbox = (image) => {
        this.lightboxImage(image);
        document.body.style.overflow = 'hidden';
        setTimeout(() => this._initLightboxTags(image), 0);
    };

    // Quick access straight from the grid tile: open the lightbox with focus already in the tags input.
    editItemTags = (image) => {
        this.openLightbox(image);
        setTimeout(() => this._focusLightboxTagsInput(), 30);
    };

    _focusLightboxTagsInput = () => {
        document.querySelector('#lightboxTagsInput')?.closest('.tagify')?.querySelector('.tagify__input')?.focus();
    };

    closeLightbox = () => {
        this.lightboxImage(null);
        document.body.style.overflow = '';
    };

    lightboxPrev = () => this._lightboxStep(-1);

    lightboxNext = () => this._lightboxStep(1);

    _lightboxStep = (direction) => {
        const current = this.lightboxImage();
        if (!current) return;

        const items = this.images();
        const index = items.findIndex(i => i.id === current.id);
        const next = items[index + direction];
        if (next) {
            this.lightboxImage(next);
            this._initLightboxTags(next);

            // Preload a few more pages as the user approaches the end.
            if (index + direction >= items.length - 5 && this.hasMore() && !this.loading()) {
                this.currentPage++;
                this.loadPage();
            }
        }
    };

    _initLightboxTags = (image) => {
        const input = document.getElementById('lightboxTagsInput');
        if (!input) return;

        if (!this._lightboxTagify) {
            this._lightboxTagify = new Tagify(input, getTagifyOptions(this._tagWhitelist));
            this._lightboxTagify.on('change', () => {
                if (this._suppressLightboxTagEvents) return;
                this._saveLightboxTags();
            });
        }

        this._suppressLightboxTagEvents = true;
        this._lightboxTagify.removeAllTags();
        this._lightboxTagify.addTags(image.tags || []);
        this._suppressLightboxTagEvents = false;
    };

    _saveLightboxTags = async () => {
        const image = this.lightboxImage();
        if (!image) return;

        const tagNames = this._lightboxTagify.value.map(t => t.value);

        try {
            const response = await fetch(`/api/images/${image.id}/tags`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ tagNames })
            });

            if (response.ok) {
                const data = await response.json();
                image.tags = data || tagNames;
                this.images.valueHasMutated();
            } else {
                toast.error('Failed to save tags.');
            }
        } catch (error) {
            console.error('Error saving tags:', error);
            toast.error('Failed to save tags.');
        }
    };

    changeLightboxCategory = async () => {
        const image = this.lightboxImage();
        if (!image) return;

        // The select's value binding already wrote the new id straight onto the image object;
        // read it back out so we know what to send (empty string means "no category").
        const categoryId = image.categoryId ? Number(image.categoryId) : null;

        try {
            const response = await fetch(`/api/images/${image.id}/category`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ categoryId })
            });

            const data = await response.json().catch(() => ({}));

            if (response.ok) {
                image.categoryId = data.categoryId ?? null;
                image.categoryName = data.categoryName ?? null;
                this.images.valueHasMutated();
                toast.success('Category updated.');
            } else {
                toast.error(data.message || 'Failed to update category.');
            }
        } catch (error) {
            console.error('Error updating category:', error);
            toast.error('Failed to update category.');
        }
    };

    deleteLightboxImage = async () => {
        const image = this.lightboxImage();
        if (!image) return;

        if (!confirm(`Delete "${image.fileName}"? This cannot be undone.`)) return;

        const deleted = await this._deleteImage(image);
        if (!deleted) return;

        const items = this.images();
        const index = items.findIndex(i => i.id === image.id);
        const next = items[index + 1] || items[index - 1] || null;

        this.images.remove(i => i.id === image.id);
        this.totalCount(Math.max(0, this.totalCount() - 1));

        if (next) {
            this.lightboxImage(next);
            this._initLightboxTags(next);
        } else {
            this.closeLightbox();
        }
    };

    // Quick delete straight from the grid tile, without opening the lightbox.
    deleteItem = async (image) => {
        if (!confirm(`Delete "${image.fileName}"? This cannot be undone.`)) return;

        const deleted = await this._deleteImage(image);
        if (!deleted) return;

        this.images.remove(i => i.id === image.id);
        this.selectedIds.remove(image.id);
        this.totalCount(Math.max(0, this.totalCount() - 1));
    };

    _deleteImage = async (image) => {
        try {
            const response = await fetch(`/api/images/${image.id}`, { method: 'DELETE' });
            const data = await response.json().catch(() => ({}));

            if (response.ok) {
                toast.success(data.message || 'Image deleted.');
                return true;
            }

            toast.error(data.message || 'Failed to delete image.');
            return false;
        } catch (error) {
            console.error('Error deleting image:', error);
            toast.error('Failed to delete image.');
            return false;
        }
    };

    _initKeyboard = () => {
        document.addEventListener('keydown', (e) => {
            if (!this.lightboxImage()) return;

            // Don't steal keys while typing in the tag input.
            const inTagInput = e.target.closest && e.target.closest('.tagify');

            if (e.key === 'Escape') {
                if (inTagInput) {
                    e.target.blur();
                } else {
                    this.closeLightbox();
                }
            } else if (!inTagInput) {
                if (e.key === 'ArrowLeft') {
                    this.lightboxPrev();
                } else if (e.key === 'ArrowRight') {
                    this.lightboxNext();
                } else if (e.key === 't') {
                    e.preventDefault();
                    this._focusLightboxTagsInput();
                } else if (e.key === 'Delete') {
                    this.deleteLightboxImage();
                }
            }
        });
    };

    // ── Drag-and-drop / paste ingestion straight from the grid page ──────

    _initDropAndPaste = () => {
        document.addEventListener('dragover', (e) => e.preventDefault());
        document.addEventListener('drop', (e) => {
            e.preventDefault();
            const files = [...(e.dataTransfer?.files || [])].filter(f => f.type.startsWith('image/'));
            if (files.length > 0) this._quickUpload(files);
        });

        document.addEventListener('paste', (e) => {
            const files = [...(e.clipboardData?.items || [])]
                .filter(i => i.kind === 'file' && i.type.startsWith('image/'))
                .map(i => i.getAsFile());
            if (files.length > 0) {
                e.preventDefault();
                this._quickUpload(files);
            }
        });
    };

    _quickUpload = async (files) => {
        for (const file of files) {
            try {
                let result = await this._uploadFile(file, false);

                if (result && result.isDuplicate) {
                    const proceed = confirm(
                        `"${file.name || 'Pasted image'}" looks like a duplicate of an existing image ` +
                        `(${result.duplicates.length} match(es) found). Save anyway?`);
                    if (!proceed) continue;
                    result = await this._uploadFile(file, true);
                }

                if (result && result.image) {
                    toast.success(`Saved "${result.image.fileName}".`);
                }
            } catch (error) {
                console.error('Quick upload failed:', error);
                toast.error(`Failed to upload "${file.name || 'image'}".`);
            }
        }

        this.reload();
    };

    _uploadFile = async (file, force) => {
        const formData = new FormData();
        formData.append('file', file, file.name || 'pasted-image.png');
        formData.append('force', force);

        const response = await fetch('/api/images/upload', { method: 'POST', body: formData });
        const data = await response.json().catch(() => ({}));

        if (!response.ok) {
            toast.error(data.message || 'Upload failed.');
            return null;
        }

        return data;
    };
}

var viewModel;

document.addEventListener('DOMContentLoaded', async () => {
    viewModel = new LibraryViewModel();
    await viewModel.loadFilterData();
    ko.applyBindings(viewModel);
    await viewModel.init();
});
