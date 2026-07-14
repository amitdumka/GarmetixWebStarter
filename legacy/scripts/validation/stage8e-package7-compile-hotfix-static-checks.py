from pathlib import Path

root = Path(__file__).resolve().parents[2]

def read(rel):
    return (root / rel).read_text(encoding='utf-8-sig')

def require(rel, text):
    data = read(rel)
    if text not in data:
        raise SystemExit(f"Missing expected text in {rel}: {text}")

def forbid(rel, text):
    data = read(rel)
    if text in data:
        raise SystemExit(f"Unexpected text in {rel}: {text}")

# Stage 14Q.2 (2026-07-10) added a real Vendor.VendorType column and wired
# TailoringEndpoints.cs to use it, superseding the workaround this script
# originally guarded (see .claude/todo.md v6.2.4 for detail). The three
# assertions that checked for the old hardcoded-label workaround were
# removed accordingly; item.VendorType is now a legitimate reference.
require('backend/Garmetix.Api/Tailoring/TailoringEndpoints.cs', 'item.VendorType == null || item.VendorType == VendorType.Tailoring')

migration = read('backend/Garmetix.Infrastructure/Data/Migrations/20260617000000_InitialFreshSchema.cs')
if '[Migration(' in migration or '[DbContext(' in migration:
    raise SystemExit('InitialFreshSchema.cs must not contain Migration/DbContext attributes; the designer file owns them.')
require('backend/Garmetix.Infrastructure/Data/Migrations/20260617000000_InitialFreshSchema.Designer.cs', '[Migration("20260617000000_InitialFreshSchema")]')
require('backend/Garmetix.Infrastructure/Data/Migrations/20260617000000_InitialFreshSchema.Designer.cs', '[DbContext(typeof(GarmetixDbContext))]')

print('Stage 8E Package 7 compile hotfix static validation passed.')
