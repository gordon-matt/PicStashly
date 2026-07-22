class TagsViewModel {
    constructor() {
        this.tags = ko.observableArray([]);
        this.loading = ko.observable(true);

        this.editingTag = ko.observable(null);
        this.editName = ko.observable('');

        this.selectedIds = ko.observableArray([]);
        this.selectedTags = ko.computed(() =>
            this.tags().filter(t => this.selectedIds().includes(t.id)));
        this.mergeTargetId = ko.observable(null);
    }

    loadTags = async () => {
        this.loading(true);
        try {
            const response = await fetch('/api/tags');
            const data = await response.json();
            this.tags(data || []);
        } catch (error) {
            console.error('Error loading tags:', error);
            toast.error('Failed to load tags.');
        } finally {
            this.loading(false);
        }
    };

    isSelected = (tag) => this.selectedIds().includes(tag.id);

    toggleSelected = (tag) => {
        if (this.isSelected(tag)) {
            this.selectedIds.remove(tag.id);
        } else {
            this.selectedIds.push(tag.id);
        }
    };

    startRename = (tag) => {
        this.editingTag(tag);
        this.editName(tag.name);
    };

    cancelRename = () => {
        this.editingTag(null);
        this.editName('');
    };

    saveRename = async () => {
        const tag = this.editingTag();
        const name = this.editName().trim();
        if (!tag || !name) return;

        try {
            const response = await fetch(`/api/tags/${tag.id}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name })
            });

            const data = await response.json().catch(() => ({}));

            if (response.ok) {
                toast.success('Tag renamed.');
                this.cancelRename();
                await this.loadTags();
            } else {
                toast.error(data.message || 'Failed to rename tag.');
            }
        } catch (error) {
            console.error('Error renaming tag:', error);
            toast.error('Failed to rename tag.');
        }
    };

    deleteTag = async (tag) => {
        if (!confirm(`Delete tag "${tag.name}"? It will be removed from ${tag.usageCount} image(s).`)) return;

        try {
            const response = await fetch(`/api/tags/${tag.id}`, { method: 'DELETE' });
            const data = await response.json().catch(() => ({}));

            if (response.ok) {
                toast.success(data.message || 'Tag deleted.');
                this.selectedIds.remove(tag.id);
                await this.loadTags();
            } else {
                toast.error(data.message || 'Failed to delete tag.');
            }
        } catch (error) {
            console.error('Error deleting tag:', error);
            toast.error('Failed to delete tag.');
        }
    };

    openMergeModal = () => {
        const selected = this.selectedTags();
        if (selected.length < 2) return;

        this.mergeTargetId(selected[0].id);
        bootstrap.Modal.getOrCreateInstance(document.getElementById('mergeModal')).show();
    };

    confirmMerge = async () => {
        const targetId = this.mergeTargetId();
        const sourceIds = this.selectedIds().filter(id => id !== targetId);
        if (!targetId || sourceIds.length === 0) return;

        try {
            const response = await fetch('/api/tags/merge', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ targetTagId: targetId, sourceTagIds: sourceIds })
            });

            const data = await response.json().catch(() => ({}));

            if (response.ok) {
                toast.success(data.message || 'Tags merged.');
                bootstrap.Modal.getInstance(document.getElementById('mergeModal')).hide();
                this.selectedIds([]);
                await this.loadTags();
            } else {
                toast.error(data.message || 'Failed to merge tags.');
            }
        } catch (error) {
            console.error('Error merging tags:', error);
            toast.error('Failed to merge tags.');
        }
    };
}

var viewModel;

document.addEventListener('DOMContentLoaded', async () => {
    viewModel = new TagsViewModel();
    ko.applyBindings(viewModel);
    await viewModel.loadTags();
});
