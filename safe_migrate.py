import glob
import re

f = glob.glob('backend/Garmetix.Infrastructure/Data/Migrations/*_AddMissingModels.cs')[0]
content = open(f, 'r').read()

# 1. Find all tables to create
tables = set(re.findall(r'migrationBuilder\.CreateTable\(\s*name:\s*"([^"]+)"', content))
table_drop_sql = "DROP TABLE IF EXISTS " + ", ".join([f'\\"{t}\\"' for t in tables]) + " CASCADE;" if tables else ""

# 2. Find all indexes to create
indexes = set(re.findall(r'migrationBuilder\.CreateIndex\(\s*name:\s*"([^"]+)"', content))
index_drop_sql = "DROP INDEX IF EXISTS " + ", ".join([f'\\"{i}\\"' for i in indexes]) + " CASCADE;" if indexes else ""

# 3. Strip AddColumn
content = re.sub(r'\s*migrationBuilder\.AddColumn[^;]+;', '', content)

# 4. Inject DROP statements at the beginning of Up
sql_injection = f'''
            migrationBuilder.Sql("{table_drop_sql}");
            migrationBuilder.Sql("{index_drop_sql}");
'''

up_marker = 'protected override void Up(MigrationBuilder migrationBuilder)\n        {\n'
idx = content.find(up_marker) + len(up_marker)

content = content[:idx] + sql_injection + content[idx:]

open(f, 'w').write(content)
print("Processed migration file. Dropping tables:", len(tables), "Dropping indexes:", len(indexes))
