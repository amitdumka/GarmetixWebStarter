import os

migration_file = 'backend/Garmetix.Infrastructure/Data/Migrations/20260705171502_CreateSaaSTables.cs'
orig = open(migration_file, 'r').read()

down_marker = 'protected override void Down(MigrationBuilder migrationBuilder)\n        {\n'
down_idx = orig.find(down_marker) + len(down_marker)
orig = orig[:down_idx] + '''
            migrationBuilder.Sql("DROP TABLE IF EXISTS \\"SaaSTokens\\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \\"TenantSubscriptions\\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \\"SaaSPlans\\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \\"SaaSClients\\";");
''' + orig[down_idx + 250:] # rough estimate to skip previous drops

# Wait, let's just replace the whole file since we know the structure!
import re

orig = re.sub(r'protected override void Down\(MigrationBuilder migrationBuilder\)\s*{\s*[\s\S]*?}', '''protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS \\"SaaSTokens\\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \\"TenantSubscriptions\\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \\"SaaSPlans\\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \\"SaaSClients\\";");
        }''', orig)

open(migration_file, 'w').write(orig)
