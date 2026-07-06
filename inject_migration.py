import os

code = open('backend/saas_filtered.txt', 'r').read()

migration_file = 'backend/Garmetix.Infrastructure/Data/Migrations/20260705171502_CreateSaaSTables.cs'
orig = open(migration_file, 'r').read()

# Replace the Up method
up_marker = 'protected override void Up(MigrationBuilder migrationBuilder)\n        {\n'
up_idx = orig.find(up_marker) + len(up_marker)
orig = orig[:up_idx] + '            ' + code + '\n' + orig[up_idx:]

down_code = '''
            migrationBuilder.DropTable(
                name: "SaaSTokens");
            migrationBuilder.DropTable(
                name: "TenantSubscriptions");
            migrationBuilder.DropTable(
                name: "SaaSPlans");
            migrationBuilder.DropTable(
                name: "SaaSClients");
'''

down_marker = 'protected override void Down(MigrationBuilder migrationBuilder)\n        {\n'
down_idx = orig.find(down_marker) + len(down_marker)
orig = orig[:down_idx] + down_code + orig[down_idx:]

open(migration_file, 'w').write(orig)
