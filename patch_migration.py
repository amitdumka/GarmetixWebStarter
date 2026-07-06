import re

path = 'backend/Garmetix.Infrastructure/Data/Migrations/20260705171502_CreateSaaSTables.cs'
with open(path, 'r') as f:
    text = f.read()

def replacer(match):
    block = match.group(0)
    if 'table: "SaaS' in block or 'table: "TenantSubscriptions"' in block:
        return block
    return re.sub('(?m)^', '// ', block)

new_text = re.sub(r'migrationBuilder\.AddColumn.*?;\s*', replacer, text, flags=re.DOTALL)
new_text = re.sub(r'migrationBuilder\.DropColumn.*?;\s*', replacer, new_text, flags=re.DOTALL)

with open(path, 'w') as f:
    f.write(new_text)

print("Migration patched successfully.")
