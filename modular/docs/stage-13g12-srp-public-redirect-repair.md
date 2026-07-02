# Stage 13G.12 SRP Public Redirect Repair

Version: 5.13.52

## Problem

Public SRP pages that were requested without the trailing slash, such as:

```text
https://srp.aadwikafashion.in/login
```

were redirected by the origin Nginx server to the internal HTTP origin:

```text
http://srp.aadwikafashion.in:8088/login/
```

That made the browser leave Cloudflare HTTPS and try the private Nginx port directly. The visible result was a timeout or a site that appeared completely broken even though `/api/health` and `/` could still return `200`.

## Fix

The SRP Nginx template now disables absolute origin redirects:

```nginx
absolute_redirect off;
port_in_redirect off;
server_name_in_redirect off;
```

With this setting, directory redirects stay relative, for example:

```text
Location: /login/
```

Cloudflare keeps the browser on:

```text
https://srp.aadwikafashion.in/login/
```

## Validation

After deploy or live Nginx reload:

```bash
curl -k -I https://srp.aadwikafashion.in/login
curl -k -I https://srp.aadwikafashion.in/pos
curl -k -I https://srp.aadwikafashion.in/api/health
```

Expected:

- `/login` and `/pos` may return `301`, but `Location` must be relative and must not contain `http://` or `:8088`.
- `/api/health` must return `200`.
- Browser navigation must remain on HTTPS public URLs.

