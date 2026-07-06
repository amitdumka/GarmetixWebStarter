import glob
import re

f = glob.glob('backend/Garmetix.Infrastructure/Data/Migrations/*_AddMissingModels.cs')[0]
content = open(f, 'r').read()

up_marker = 'protected override void Up(MigrationBuilder migrationBuilder)\n        {\n'
down_marker = '        protected override void Down(MigrationBuilder migrationBuilder)'
up_idx = content.find(up_marker) + len(up_marker)
down_idx = content.find(down_marker)

# I will just use my previous saas_filtered.txt which contains exactly the CreateTable and CreateIndex for the 4 tables!
saas_code = open('backend/saas_filtered.txt', 'r').read()

# I also need to replace the Down method to only drop the 4 tables.
down_code = '''
            migrationBuilder.DropTable(name: "SaaSTokens");
            migrationBuilder.DropTable(name: "TenantSubscriptions");
            migrationBuilder.DropTable(name: "SaaSPlans");
            migrationBuilder.DropTable(name: "SaaSClients");
'''

new_content = content[:up_idx] + '            ' + saas_code + '\n        }\n\n' + down_marker + '\n        {\n' + down_code + '\n        }\n    }\n}\n'

open(f, 'w').write(new_content)
print('Replaced Up method with SaaS tables only.')
