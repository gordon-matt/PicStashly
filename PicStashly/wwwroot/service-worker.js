// Minimal service worker: required for PWA installability and the Web Share Target.
// All requests pass straight through to the network — the app is server-rendered
// and image data should never be stale.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) => event.waitUntil(clients.claim()));
self.addEventListener('fetch', () => { /* network passthrough */ });
