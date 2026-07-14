# GSTIN Provider Validation v4.6.4

Configure these values in `.env.production`:

```env
GSTIN_LOOKUP_ENABLED=true
GSTIN_LOOKUP_BASE_URL=https://your-provider.example/api/gstin
GSTIN_LOOKUP_URL_TEMPLATE=
GSTIN_LOOKUP_API_KEY=your-provider-api-key
GSTIN_LOOKUP_API_KEY_HEADER=x-api-key
GSTIN_LOOKUP_SOURCE_NAME=Your Provider Name
```

Some providers require a full URL template instead of a base URL. In that case use:

```env
GSTIN_LOOKUP_URL_TEMPLATE=https://your-provider.example/api/gstin/{gstin}
```

After deployment, open **Production Readiness** and run **GSTIN provider validation** with a known GSTIN. Also run:

```bash
cd /opt/garmetix/current
./scripts/linux/gstin-provider-readiness-check.sh .env.production
```
