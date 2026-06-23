from pathlib import Path

root = Path(__file__).resolve().parents[2]

def require(path: str, needle: str) -> None:
    text = (root / path).read_text()
    if needle not in text:
        raise SystemExit(f"Missing expected text in {path}: {needle}")

require('backend/Garmetix.Api/Dashboard/DashboardDtos.cs', 'PartyDueDashboardRowDto')
require('backend/Garmetix.Api/Dashboard/DashboardDtos.cs', 'CashPaymentSummaryDto')
require('backend/Garmetix.Api/Dashboard/DashboardDtos.cs', 'StoreGroupComparisonViewDto')
require('backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs', 'CustomerDueDashboardAsync')
require('backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs', 'VendorDueDashboardAsync')
require('backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs', 'CashPaymentSummaryAsync')
require('backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs', 'StoreGroupComparisonDashboardAsync')
require('frontend/garmetix-web/pages/dashboard/business/index.vue', 'Customer dues')
require('frontend/garmetix-web/pages/dashboard/business/index.vue', 'Vendor dues')
require('frontend/garmetix-web/pages/dashboard/business/index.vue', 'Cash and payment summary')
require('frontend/garmetix-web/pages/dashboard/business/index.vue', 'Store group cash / due comparison')
require('docs/planning/CURRENT-ROADMAP.md', 'Stage 8E Package 6 Due and Payment Dashboards / v4.4.3')
require('docs/planning/CURRENT-ROADMAP.md', 'customer/vendor due dashboards, cash/payment summaries, and store-group comparison views in v4.4.3')
print('Stage 8E Package 6 static validation passed.')
