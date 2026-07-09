export const garmetixModularVersion = {
  version: '6.0.66',
  stage: 'Stage 14N.1 BooksMasterTable Props Hotfix',
  label: 'Version6 Stage 14N.1 BooksMasterTable Props Hotfix',
  summary: 'Books: hotfix for a regression introduced by the 6.0.65 BooksMasterTable fix - the reactive-destructure-with-defaults form (const { columns = [] } = defineProps<{...}>()) applied defaults in the script but not in the compiled inline-template render function in this build, so columns/rows read as undefined on first render and crashed the whole Books app during init ("Cannot read properties of undefined (reading length)"), blanking every page. Replaced with an explicit JS-object runtime defineProps({ columns: { type: Array, default: () => [] }, ... }) declaration plus defensive Array.isArray-guarded computeds in the template - verified the compiled bundle now has an explicit props:{...} options entry (absent in both prior attempts) and the render function goes through the guarded computeds everywhere, so it cannot crash regardless of what the parent passes.'
} as const
