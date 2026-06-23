from pathlib import Path
root = Path(__file__).resolve().parents[2]

def require(path: str, needle: str) -> None:
    text = (root / path).read_text()
    if needle not in text:
        raise SystemExit(f"Missing expected text in {path}: {needle}")

require('backend/Garmetix.Domain/Generated/Enums/BharatEnums.cs', 'TailoringOrderType')
require('backend/Garmetix.Domain/Generated/Enums/BharatEnums.cs', 'TailoringCostResponsibility')
require('backend/Garmetix.Domain/Generated/Models/Inventory/Tailoring.cs', 'TailoringOrder')
require('backend/Garmetix.Domain/Generated/Models/Inventory/Tailoring.cs', 'TailoringServiceItem')
require('backend/Garmetix.Domain/Generated/Models/Inventory/Tailoring.cs', 'TailoringCustomerReceipt')
require('backend/Garmetix.Domain/Generated/Models/Inventory/Tailoring.cs', 'TailoringVendorPayment')
require('backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs', 'DbSet<TailoringOrder>')
require('backend/Garmetix.Api/Tailoring/TailoringEndpoints.cs', 'MapTailoringEndpoints')
require('backend/Garmetix.Api/Tailoring/TailoringEndpoints.cs', 'convert-to-service-invoice')
require('backend/Garmetix.Api/Tailoring/TailoringEndpoints.cs', 'receive-payment')
require('backend/Garmetix.Api/Tailoring/TailoringEndpoints.cs', 'pay-vendor')
require('backend/Garmetix.Api/Program.cs', 'app.MapTailoringEndpoints();')
require('frontend/garmetix-web/pages/tailoring/index.vue', 'Tailoring & Alteration')
require('frontend/garmetix-web/pages/tailoring/index.vue', 'Order history and status')
require('frontend/garmetix-web/pages/tailoring/index.vue', 'Convert to service invoice')
require('frontend/garmetix-web/components/AppShell.vue', '/tailoring')
require('docs/planning/CURRENT-ROADMAP.md', 'Stage 8E Package 7 Tailoring and Alteration Workflow / v4.4.4')
require('docs/planning/CURRENT-ROADMAP.md', 'order -> delivery -> service invoice -> payment')
print('Stage 8E Package 7 static validation passed.')
