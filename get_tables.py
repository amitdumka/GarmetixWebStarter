import glob
import re

f = glob.glob('backend/Garmetix.Infrastructure/Data/Migrations/*_AddMissingModels.cs')[0]
content = open(f, 'r').read()

tables = set(re.findall(r'migrationBuilder\.CreateTable\(\s*name:\s*"([^"]+)"', content))
print("Tables:", tables)

# We want to DROP these tables in the database before running the migration, so they don't cause "already exists" errors.
# We will just write a SQL script and execute it via the Up method.
# Wait, we can't execute it via the Up method BEFORE the CreateTable calls if we just modify the DB directly.

sql = "DROP TABLE IF EXISTS " + ", ".join([f'"{t}"' for t in tables]) + " CASCADE;"
print(sql)

