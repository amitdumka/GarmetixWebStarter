# Seeder Compile Hotfix v4.9.6

After installing this package, rebuild:

```bash
cd /opt/garmetix/current
docker compose down
docker compose up -d --build
```

The previous API errors were in:

```text
backend/Garmetix.Api/Seeds/PortableSeederEndpoints.cs
backend/Garmetix.Api/Seeds/CompanyMergeEndpoints.cs
backend/Garmetix.Api/Seeds/AfssDefaultSeederService.cs
```

`#18 CANCELED` was a Docker side effect, not the root cause.
