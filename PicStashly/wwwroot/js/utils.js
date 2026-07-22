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

/**
 * Copies the image at `url` to the OS clipboard so it can be pasted directly into social
 * media / chat apps. Browsers only reliably support "image/png" clipboard writes (jpg/webp
 * are inconsistent or unsupported), so the source is always re-encoded through a canvas.
 */
export async function copyImageToClipboard(url) {
    if (!navigator.clipboard || typeof ClipboardItem === 'undefined') {
        throw new Error('Copying images is not supported in this browser.');
    }

    const response = await fetch(url);
    if (!response.ok) {
        throw new Error('Failed to load image.');
    }

    const pngBlob = await blobToPngBlob(await response.blob());
    await navigator.clipboard.write([new ClipboardItem({ 'image/png': pngBlob })]);
}

function blobToPngBlob(sourceBlob) {
    return new Promise((resolve, reject) => {
        const objectUrl = URL.createObjectURL(sourceBlob);
        const img = new Image();

        img.onload = () => {
            const canvas = document.createElement('canvas');
            canvas.width = img.naturalWidth;
            canvas.height = img.naturalHeight;
            canvas.getContext('2d').drawImage(img, 0, 0);

            canvas.toBlob((pngBlob) => {
                URL.revokeObjectURL(objectUrl);
                if (pngBlob) {
                    resolve(pngBlob);
                } else {
                    reject(new Error('Failed to encode image.'));
                }
            }, 'image/png');
        };

        img.onerror = () => {
            URL.revokeObjectURL(objectUrl);
            reject(new Error('Failed to decode image.'));
        };

        img.src = objectUrl;
    });
}
