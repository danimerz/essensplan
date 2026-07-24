import express from 'express';
import { MigrosAPI } from 'migros-api-wrapper';

const PORT = process.env.PORT ?? 3001;
const app = express();

// Cache guest token (valid ~1h)
let _token = null;
let _tokenExpiry = 0;

async function getToken() {
  if (_token && Date.now() < _tokenExpiry) return _token;
  console.log('[migros] Fetching guest token...');
  const info = await MigrosAPI.account.oauth2.getGuestToken();
  _token = info.token;
  _tokenExpiry = Date.now() + 50 * 60 * 1000; // 50 min
  console.log('[migros] Guest token acquired.');
  return _token;
}

function resolveImageUrl(url) {
  if (!url) return null;
  // Replace Rokka {stack} placeholder with a 200x200 stack
  return url.replace('{stack}', 'fl-w200-h200');
}

// Simple in-process image cache (per server run)
const imageCache = new Map();

app.get('/image', async (req, res) => {
  const q = String(req.query.q ?? '').trim();
  if (!q) return res.json({ imageUrl: null });

  if (imageCache.has(q)) {
    return res.json({ imageUrl: imageCache.get(q) });
  }

  try {
    const token = await getToken();

    // Step 1: search for product IDs
    const searchResult = await MigrosAPI.products.productSearch.searchProduct(
      { query: q, language: 'de' },
      { leshopch: token }
    );
    const firstId = searchResult?.productIds?.[0];
    if (!firstId) {
      console.log(`[migros] "${q}" → no product IDs found`);
      imageCache.set(q, null);
      return res.json({ imageUrl: null });
    }

    // Step 2: get product card with image
    const cards = await MigrosAPI.products.productDisplay.getProductCards(
      { productFilter: { uids: [firstId] } },
      { leshopch: token }
    );
    const card = Array.isArray(cards) ? cards[0] : null;
    const rawUrl = card?.images?.[0]?.url ?? card?.imageTransparent?.url ?? null;
    const imageUrl = resolveImageUrl(rawUrl);

    console.log(`[migros] "${q}" → ${imageUrl ?? 'no image'}`);
    imageCache.set(q, imageUrl);
    return res.json({ imageUrl });
  } catch (err) {
    console.error(`[migros] Error for "${q}":`, err?.message ?? err);
    return res.json({ imageUrl: null });
  }
});

// Health check
app.get('/health', (_req, res) => res.json({ ok: true }));

app.listen(PORT, () => {
  console.log(`Migros image server running on http://localhost:${PORT}`);
  console.log('Endpoints: GET /image?q=Butter  |  GET /health');
});
