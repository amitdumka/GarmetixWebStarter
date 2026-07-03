<template>
  <HrPlaceholder
    title="Mobile Kiosk Rehearsal"
    description="Rehearsal mode will let operators test mobile attendance without affecting final attendance."
    :items="['Dry run', 'Validation log', 'Operator signoff']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Mobile Kiosk Rehearsal - Garmetix HR' })
</script>
