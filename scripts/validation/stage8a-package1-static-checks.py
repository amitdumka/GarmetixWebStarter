from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []


def check(name: str, condition: bool):
    checks.append((name, condition))


audit_page = (root / "frontend/garmetix-web/pages/ui-audit/index.vue").read_text(encoding="utf-8")
audit_progress = (root / "frontend/garmetix-web/composables/useUiAuditProgress.ts").read_text(encoding="utf-8")
register_panel = (root / "frontend/garmetix-web/components/ui/RegisterPanel.vue").read_text(encoding="utf-8")
credit_notes = (root / "frontend/garmetix-web/pages/credit-notes/index.vue").read_text(encoding="utf-8")
debit_notes = (root / "frontend/garmetix-web/pages/debit-notes/index.vue").read_text(encoding="utf-8")
styles = (root / "frontend/garmetix-web/assets/css/main.css").read_text(encoding="utf-8")

check("audit progress uses a versioned browser key", "garmetix.ui-audit.v4.0.0" in audit_progress)
check("audit progress supports all three states", all(value in audit_progress for value in ("pending", "in-progress", "reviewed")))
check("audit queue has module and status filters", "moduleFilter" in audit_page and "statusFilter" in audit_page)
check("audit queue supports notes", "updateNote" in audit_page)
check("audit queue opens the selected page", "router.push(row.route)" in audit_page)
check("shared register handles loading", "register-panel-loading" in register_panel)
check("shared register handles errors and retry", 'v-if="error"' in register_panel and "emit('retry')" in register_panel)
check("shared register handles empty data", 'v-else-if="empty"' in register_panel)
check("credit notes use shared register and search", "UiRegisterPanel" in credit_notes and "filteredNotes" in credit_notes)
check("debit notes use shared register and search", "UiRegisterPanel" in debit_notes and "filteredNotes" in debit_notes)
check("register layout has a mobile rule", "@media (max-width: 700px)" in styles and ".register-panel-header" in styles)

failed = [name for name, passed in checks if not passed]
for name, passed in checks:
    print(f"[{'PASS' if passed else 'FAIL'}] {name}")

if failed:
    raise SystemExit(f"{len(failed)} Stage 8A Package 1 check(s) failed")

print(f"Stage 8A Package 1 static validation passed: {len(checks)} checks")
