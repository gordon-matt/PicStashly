export const delay = (ms) =>
    new Promise(resolve => setTimeout(resolve, ms));

export function formatDate(dateString) {
    if (!dateString) return 'N/A';
    var date = new Date(dateString);
    return date.toLocaleDateString();
}

export function formatDateTime(dateString) {
    if (!dateString) return 'N/A';
    var date = new Date(dateString);
    return date.toLocaleString();
}

export function formatFileSize(bytes, decimals = 2) {
    if (!bytes && bytes !== 0) return 'N/A';
    if (bytes === 0) return '0 B';

    const sizes = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    const value = bytes / Math.pow(1024, i);

    return `${value.toFixed(decimals)} ${sizes[i]}`;
}

export function formatNumber(num) {
    if (!num) return '0';
    return num.toLocaleString();
}
