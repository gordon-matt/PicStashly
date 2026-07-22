class CategoriesViewModel {
    constructor() {
        this.categories = ko.observableArray([]);
        this.loading = ko.observable(true);

        this.newCategoryName = ko.observable('');

        this.editingCategory = ko.observable(null);
        this.editName = ko.observable('');
    }

    loadCategories = async () => {
        this.loading(true);
        try {
            const response = await fetch('/api/categories');
            const data = await response.json();
            this.categories(data || []);
        } catch (error) {
            console.error('Error loading categories:', error);
            toast.error('Failed to load categories.');
        } finally {
            this.loading(false);
        }
    };

    createCategory = async () => {
        const name = this.newCategoryName().trim();
        if (!name) return;

        try {
            const response = await fetch('/api/categories', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name })
            });

            const data = await response.json().catch(() => ({}));

            if (response.ok) {
                toast.success('Category created.');
                this.newCategoryName('');
                await this.loadCategories();
            } else {
                toast.error(data.message || 'Failed to create category.');
            }
        } catch (error) {
            console.error('Error creating category:', error);
            toast.error('Failed to create category.');
        }
    };

    startRename = (category) => {
        this.editingCategory(category);
        this.editName(category.name);
    };

    cancelRename = () => {
        this.editingCategory(null);
        this.editName('');
    };

    saveRename = async () => {
        const category = this.editingCategory();
        const name = this.editName().trim();
        if (!category || !name) return;

        try {
            const response = await fetch(`/api/categories/${category.id}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name })
            });

            const data = await response.json().catch(() => ({}));

            if (response.ok) {
                toast.success('Category renamed.');
                this.cancelRename();
                await this.loadCategories();
            } else {
                toast.error(data.message || 'Failed to rename category.');
            }
        } catch (error) {
            console.error('Error renaming category:', error);
            toast.error('Failed to rename category.');
        }
    };

    deleteCategory = async (category) => {
        if (!confirm(`Delete category "${category.name}"? Its ${category.imageCount} image(s) will become uncategorized.`)) return;

        try {
            const response = await fetch(`/api/categories/${category.id}`, { method: 'DELETE' });
            const data = await response.json().catch(() => ({}));

            if (response.ok) {
                toast.success('Category deleted.');
                await this.loadCategories();
            } else {
                toast.error(data.message || 'Failed to delete category.');
            }
        } catch (error) {
            console.error('Error deleting category:', error);
            toast.error('Failed to delete category.');
        }
    };
}

var viewModel;

document.addEventListener('DOMContentLoaded', async () => {
    viewModel = new CategoriesViewModel();
    ko.applyBindings(viewModel);
    await viewModel.loadCategories();
});
