text = open('backend/saas_migration_code.txt', 'r').read()

def extract_block(name, kind):
    start_idx = text.find(kind + '(\n                name: "' + name + '"')
    if start_idx == -1:
        return ''
    
    # count braces/parentheses
    idx = start_idx
    paren_count = 0
    started = False
    
    while idx < len(text):
        if text[idx] == '(':
            paren_count += 1
            started = True
        elif text[idx] == ')':
            paren_count -= 1
        
        if started and paren_count == 0:
            return text[start_idx:idx+2]
        idx += 1
    return ''

tables = ['SaaSClients', 'SaaSPlans', 'TenantSubscriptions', 'SaaSTokens']
indexes = [
    ('IX_SaaSTokens_SaaSClientId', 'SaaSTokens'),
    ('IX_SaaSTokens_SaaSPlanId', 'SaaSTokens'),
    ('IX_TenantSubscriptions_CompanyId', 'TenantSubscriptions')
]

out = []
for t in tables:
    out.append(extract_block(t, 'migrationBuilder.CreateTable'))

for i, t in indexes:
    out.append(extract_block(i, 'migrationBuilder.CreateIndex'))

open('backend/saas_filtered.txt', 'w').write('\n\n            '.join(out))
