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

require('backend/Garmetix.Api/Tailoring/TailoringEndpoints.cs', 'expose active vendors for tailoring/alteration assignment')
require('backend/Garmetix.Api/Tailoring/TailoringEndpoints.cs', 'new TailoringVendorDto(item.Id, item.Name, item.MobileNumber, "Vendor"')
forbid('backend/Garmetix.Api/Tailoring/TailoringEndpoints.cs', 'item.VendorType')

migration = read('backend/Garmetix.Infrastructure/Data/Migrations/20260617000000_InitialFreshSchema.cs')
if '[Migration(' in migration or '[DbContext(' in migration:
    raise SystemExit('InitialFreshSchema.cs must not contain Migration/DbContext attributes; the designer file owns them.')
require('backend/Garmetix.Infrastructure/Data/Migrations/20260617000000_InitialFreshSchema.Designer.cs', '[Migration("20260617000000_InitialFreshSchema")]')
require('backend/Garmetix.Infrastructure/Data/Migrations/20260617000000_InitialFreshSchema.Designer.cs', '[DbContext(typeof(GarmetixDbContext))]')

print('Stage 8E Package 7 compile hotfix static validation passed.')
