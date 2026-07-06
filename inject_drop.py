import glob
import re

f = glob.glob('backend/Garmetix.Infrastructure/Data/Migrations/*_AddMissingModels.cs')[0]
content = open(f, 'r').read()

tables = set(re.findall(r'migrationBuilder\.CreateTable\(\s*name:\s*"([^"]+)"', content))

sql = 'migrationBuilder.Sql("DROP TABLE IF EXISTS ' + ", ".join([f'\\"{t}\\"' for t in tables]) + ' CASCADE;");'

up_marker = 'protected override void Up(MigrationBuilder migrationBuilder)\n        {\n'
idx = content.find(up_marker) + len(up_marker)

content = content[:idx] + '            ' + sql + '\n' + content[idx:]
open(f, 'w').write(content)
print('Injected DROP TABLE IF EXISTS.')
