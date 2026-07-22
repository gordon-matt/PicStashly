// Shared helper for pages that need the category list for a <select> dropdown
// (library filter, upload batch category, image details/lightbox reassignment).
export async function loadCategories() {
    try {
        const response = await fetch('/api/categories');
        const data = await response.json();
        return data || [];
    } catch (e) {
        console.error('Error loading categories', e);
        return [];
    }
}
