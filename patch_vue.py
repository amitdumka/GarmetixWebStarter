import sys

path = 'frontend/modular/apps/admin/pages/saas-manager.vue'
with open(path, 'r') as f:
    text = f.read()

# Replace UFormGroup with UFormField
text = text.replace('<UFormGroup', '<UFormField').replace('</UFormGroup>', '</UFormField>')

# Replace UVerticalNavigation with UNavigationMenu
text = text.replace('<UVerticalNavigation', '<UNavigationMenu orientation="vertical"').replace('</UVerticalNavigation>', '</UNavigationMenu>')

# Replace :links= with :items= in UNavigationMenu
text = text.replace(':links="navigation"', ':items="navigation"')

# Replace key: with id: and label: with header: in columns
text = text.replace('key:', 'accessorKey:').replace('label:', 'header:')

with open(path, 'w') as f:
    f.write(text)
