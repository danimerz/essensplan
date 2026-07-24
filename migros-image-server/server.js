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
  _token = info.token ?? info.access_token ?? info;
  _tokenExpiry = Date.now() + 50 * 60 * 1000; // 50 min
  console.log('[migros] Guest token acquired.');
  return _token;
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
    const result = await MigrosAPI.products.productSearch.postProductSearch(
      { query: q, language: 'de' },
      { leshopceh: token }
    );

    // Log first result so we can inspect the shape during development
    if (result?.articles?.length) {
      console.log(`[migros] "${q}" → first article:`, JSON.stringify(result.articles[0], null, 2));
    } else {
      console.log(`[migros] "${q}" → no articles`, JSON.stringify(result, null, 2));
    }

    const article = result?.articles?.[0];
    const imageUrl = article?.imageLarge ?? article?.imageSmall ?? article?.image ?? null;

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
