#!/usr/bin/env python3
from pathlib import Path

root = Path(__file__).resolve().parents[2]
errors: list[str] = []

migrations_dir = root / "backend/Garmetix.Infrastructure/Data/Migrations"
migrations = sorted(p.name for p in migrations_dir.glob("*.cs"))
expected = sorted([
    "20260617000000_InitialFreshSchema.cs",
    "20260617000000_InitialFreshSchema.Designer.cs",
    "GarmetixDbContextModelSnapshot.cs",
])
if migrations != expected:
    errors.append(f"Unexpected migration files: {migrations}")

checks = [
    ("backend/Garmetix.Infrastructure/Data/Migrations/20260617000000_InitialFreshSchema.cs", "[Migration(\"20260617000000_InitialFreshSchema\")]"),
    ("backend/Garmetix.Infrastructure/Data/Migrations/20260617000000_InitialFreshSchema.Designer.cs", "protected override void BuildTargetModel(ModelBuilder modelBuilder)"),
    ("backend/Garmetix.Infrastructure/Data/Migrations/GarmetixDbContextModelSnapshot.cs", "partial class GarmetixDbContextModelSnapshot : ModelSnapshot"),
    ("backend/Garmetix.Api/Program.cs", "Database:SchemaBootstrapMode"),
    ("backend/Garmetix.Api/Program.cs", "EnsureCreatedAsync"),
    ("backend/Garmetix.Api/Program.cs", "20260617000000_InitialFreshSchema"),
    ("docker-compose.prod.yml", "Database__SchemaBootstrapMode"),
    ("deploy/create-production-env.sh", "DATABASE_SCHEMA_BOOTSTRAP_MODE=FreshBaseline"),
    ("deploy/create-production-env.sh", "RESET_DATABASE_ON_DEPLOY=false"),
    ("deploy/run-production.sh", "down --remove-orphans --volumes"),
    ("deploy/reset-production-database.sh", "down --remove-orphans --volumes"),
    ("deploy/deploy-to-macmini.sh", "RESET_DATABASE_ON_DEPLOY"),
    ("frontend/garmetix-web/Dockerfile", "npm install --no-save @iconify-json/lucide"),
    ("docs/operations/Clean-Initial-Migration-v4.3.9.md", "Historical migration files were removed"),
]
for relative, marker in checks:
    path = root / relative
    if not path.exists():
        errors.append(f"missing file: {relative}")
        continue
    text = path.read_text(encoding="utf-8")
    if marker not in text:
        errors.append(f"missing marker in {relative}: {marker}")

if errors:
    print("Clean initial migration validation failed:")
    for error in errors:
        print(f"- {error}")
    raise SystemExit(1)

print("Clean initial migration static validation passed.")
