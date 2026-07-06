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

# 3. Find all columns to add
columns = re.findall(r'migrationBuilder\.AddColumn[^;]+table:\s*"([^"]+)",\s*name:\s*"([^"]+)"', content, flags=re.DOTALL)
# some AddColumn calls have name first, table second
# Let's just use a simpler regex
cols = []
for match in re.finditer(r'migrationBuilder\.AddColumn.*?\(.*?\);', content, flags=re.DOTALL):
    text = match.group(0)
    table_match = re.search(r'table:\s*"([^"]+)"', text)
    name_match = re.search(r'name:\s*"([^"]+)"', text)
    if table_match and name_match:
        cols.append((table_match.group(1), name_match.group(1)))

column_drop_sql = ""
for t, c in cols:
    column_drop_sql += f'ALTER TABLE \\"{t}\\" DROP COLUMN IF EXISTS \\"{c}\\" CASCADE; '

# 4. Inject DROP statements at the beginning of Up
sql_injection = f'''
            migrationBuilder.Sql("{table_drop_sql}");
            migrationBuilder.Sql("{index_drop_sql}");
            migrationBuilder.Sql("{column_drop_sql}");
'''

up_marker = 'protected override void Up(MigrationBuilder migrationBuilder)\n        {\n'
idx = content.find(up_marker) + len(up_marker)

content = content[:idx] + sql_injection + content[idx:]

open(f, 'w').write(content)
print("Processed migration file. Dropping tables:", len(tables), "Dropping indexes:", len(indexes), "Dropping columns:", len(cols))
