/**
 * Shared Tagify options so Enter prefers adding the typed tag (instead of auto-selecting
 * the first suggestion), while still allowing multi-word tags.
 * Use getTagifyOptions(whitelist, overrides) when creating Tagify instances.
 */

/**
 * @param {string[]} whitelist - Tag names for autocomplete
 * @param {object} overrides - Optional overrides (e.g. maxTags, dropdown)
 * @returns {object} Tagify options
 */
export function getTagifyOptions(whitelist = [], overrides = {}) {
    return {
        whitelist: whitelist || [],
        enforceWhitelist: false,
        maxTags: 20,
        ...overrides,
        dropdown: {
            maxItems: 20,
            classname: 'tags-look',
            enabled: 1,
            closeOnSelect: false,
            highlightFirst: false,   // do not auto-highlight first suggestion; Enter keeps typed value
            ...(overrides.dropdown || {})
        }
    };
}

/** Loads all tag names for autocomplete whitelists. */
export async function loadTagWhitelist() {
    try {
        const response = await fetch('/api/tags');
        const data = await response.json();
        return (data.tags || []).map(t => t.name);
    } catch (e) {
        console.error('Error loading tag whitelist', e);
        return [];
    }
}
