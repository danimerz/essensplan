import express from 'express';
import { MigrosAPI } from 'migros-api-wrapper';

const PORT = process.env.PORT ?? 3001;
const app = express();

// Guest token cache (50 min)
let _token = null;
let _tokenExpiry = 0;

async function getToken() {
  if (_token && Date.now() < _tokenExpiry) return _token;
  console.log('[migros] Fetching guest token...');
  const info = await MigrosAPI.account.oauth2.getGuestToken();
  _token = info.token;
  _tokenExpiry = Date.now() + 50 * 60 * 1000;
  console.log('[migros] Guest token acquired.');
  return _token;
}

// Migros category ID → our app category names
const CATEGORY_MAP = {
  7494732: 'Gemüse & Früchte',
  7494731: 'Milch & Milchprodukte',
  7494730: 'Fleisch & Fisch',
  7494733: 'Brot & Backwaren',
  7494735: 'Pasta, Reis & Körner',
  7494766: 'Süsses & Backzutaten',
  7494767: 'Pasta, Reis & Körner',
  7494768: 'Süsses & Backzutaten',
  7494734: 'Getränke',
  7494737: 'Getränke',
  7494736: 'Süsses & Backzutaten',
  7494738: 'Tiefkühlprodukte',
};

// Promotion cache (refreshed daily)
let _promoIds = new Set();
let _promoExpiry = 0;

async function getPromoIds(token) {
  if (Date.now() < _promoExpiry) return _promoIds;
  try {
    console.log('[migros] Fetching weekly promotions...');
    const promos = await MigrosAPI.products.productDisplay.getProductPromotionSearch(
      { language: 'de', storeType: 'OFFLINE', region: 'national' },
      { leshopch: token }
    );
    _promoIds = new Set(
      (promos.items ?? []).filter(i => i.type === 'PRODUCT').map(i => i.id)
    );
    _promoExpiry = Date.now() + 24 * 60 * 60 * 1000;
    console.log(`[migros] ${_promoIds.size} Aktionsprodukte geladen (bis ${promos.endDate}).`);
  } catch (e) {
    console.error('[migros] Promotions konnten nicht geladen werden:', e.message);
  }
  return _promoIds;
}

function resolveImageUrl(url) {
  if (!url) return null;
  return url.replace('{stack}', 'original');
}

const imageCache = new Map();

app.get('/image', async (req, res) => {
  const q = String(req.query.q ?? '').trim();
  if (!q) return res.json({ imageUrl: null, category: null, price: null, isPromotion: false });

  if (imageCache.has(q)) return res.json(imageCache.get(q));

  try {
    const token = await getToken();
    const promoIds = await getPromoIds(token);

    // Step 1: search for product IDs
    const searchResult = await MigrosAPI.products.productSearch.searchProduct(
      { query: q, language: 'de' },
      { leshopch: token }
    );
    const firstId = searchResult?.productIds?.[0];
    if (!firstId) {
      const result = { imageUrl: null, category: null, price: null, isPromotion: false };
      imageCache.set(q, result);
      return res.json(result);
    }

    // Step 2: get product card (image + category + price)
    const cards = await MigrosAPI.products.productDisplay.getProductCards(
      { productFilter: { uids: [firstId] } },
      { leshopch: token }
    );
    const card = Array.isArray(cards) ? cards[0] : null;

    const rawUrl = card?.images?.[0]?.url ?? card?.imageTransparent?.url ?? null;
    const imageUrl = resolveImageUrl(rawUrl);

    const breadcrumbId = parseInt(card?.breadcrumb?.[0]?.id ?? '0');
    const category = CATEGORY_MAP[breadcrumbId] ?? null;

    const price = card?.offer?.price?.effectiveDisplayValue ?? null;

    const isPromotion = promoIds.has(firstId);

    console.log(`[migros] "${q}" → ${category ?? 'unbekannt'}, ${price ? price + ' CHF' : '–'}${isPromotion ? ' 🏷️' : ''}`);

    const result = { imageUrl, category, price, isPromotion };
    imageCache.set(q, result);
    return res.json(result);
  } catch (err) {
    console.error(`[migros] Fehler für "${q}":`, err?.message ?? err);
    return res.json({ imageUrl: null, category: null, price: null, isPromotion: false });
  }
});

app.get('/health', (_req, res) => res.json({ ok: true }));

app.listen(PORT, () => {
  console.log(`Migros image server läuft auf http://localhost:${PORT}`);
  console.log('Endpoints: GET /image?q=Butter  |  GET /health');
});
