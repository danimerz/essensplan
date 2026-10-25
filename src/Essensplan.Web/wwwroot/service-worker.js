// Minimal "app shell" service worker. It exists so the browser considers the site
// installable and so the static assets (CSS, icons) load instantly on repeat visits.
// It deliberately does NOT intercept page navigations, the Blazor Server SignalR
// circuit, or any API calls — Blazor Server needs a live connection to the backend
// for every interaction, so there is no meaningful "offline mode" to fake here.

const CACHE_NAME = 'essensplan-shell-v2';

const SHELL_ASSETS = [
  '/app.css',
  '/css/tokens.css',
  '/css/base.css',
  '/css/layout.css',
  '/css/components.css',
  '/js/theme.js',
  '/manifest.json',
  '/icons/icon-192.png',
  '/icons/icon-512.png',
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then((cache) => cache.addAll(SHELL_ASSETS))
      .catch(() => {})
  );
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key)))
    )
  );
  self.clients.claim();
});

self.addEventListener('fetch', (event) => {
  if (event.request.method !== 'GET') return;

  const url = new URL(event.request.url);
  if (url.origin !== self.location.origin) return;
  if (!SHELL_ASSETS.includes(url.pathname)) return;

  // Stale-while-revalidate: serve the cached copy immediately for speed, but always
  // fetch a fresh one in the background and update the cache for next time. A pure
  // cache-first strategy here previously meant a CSS fix never reached installed PWAs
  // until something happened to change this very file's bytes (which is what makes
  // the browser notice there's a new service worker to install in the first place).
  event.respondWith(
    caches.open(CACHE_NAME).then(async (cache) => {
      const cached = await cache.match(event.request);
      const network = fetch(event.request)
        .then((response) => {
          if (response.ok) cache.put(event.request, response.clone());
          return response;
        })
        .catch(() => cached);
      return cached || network;
    })
  );
});
