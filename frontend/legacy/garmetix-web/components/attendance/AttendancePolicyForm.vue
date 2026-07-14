<script setup lang="ts">
const model = defineModel<any>({ default: () => ({ name: 'Default Attendance Policy', graceMinutes: 10, duplicateWindowMinutes: 5, active: true }) })
</script>
<template>
  <div class="grid gap-3 md:grid-cols-3">
    <UInput v-model="model.name" placeholder="Policy name" />
    <UInput v-model="model.graceMinutes" type="number" placeholder="Grace minutes" />
    <UInput v-model="model.duplicateWindowMinutes" type="number" placeholder="Duplicate window" />
  </div>
</template>
