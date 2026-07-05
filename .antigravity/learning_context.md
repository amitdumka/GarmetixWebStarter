# Project Learning Context

## Overview
Garmetix is a multi-tenant SaaS application containing:
1. **Backend**: ASP.NET Core API with Entity Framework Core (multi-tenant isolated by TenantId).
2. **Frontend**: Nuxt 3 modular architecture consisting of various apps (admin, pos, hr, inventory, books) and shared packages (shared-ui, shared-auth).
3. **Legacy**: Older Vue/Nuxt application being migrated piece by piece.