<script setup lang="ts">
const props = defineProps<{ employees: any[], loading?: boolean }>()
const emit = defineEmits<{ submit: [body: any] }>()
const form = reactive<any>({ employeeId: '', punchType: 'Auto', reason: '', source: 'Manual' })
const activeEmployees = computed(() => props.employees.filter((employee: any) => {
  const status = String(employee?.employeeStatus || '').trim().toLowerCase()
  return Boolean(employee?.working) && !['resigned', 'terminated', 'inactive'].includes(status)
}))
function submit() { emit('submit', { ...form }) }
</script>
<template>
  <UCard>
    <template #header><strong>Manual Punch</strong></template>
    <div class="grid gap-3 md:grid-cols-4">
      <USelect v-model="form.employeeId" :items="activeEmployees.map(e => ({ value: e.id, label: `${e.employeeCode || 'EMP'} - ${e.firstName || ''} ${e.lastName || ''}` }))" placeholder="Employee" />
      <USelect v-model="form.punchType" :items="['Auto','CheckIn','CheckOut','BreakIn','BreakOut']" />
      <UInput v-model="form.reason" placeholder="Reason" />
      <UButton label="Save Punch" :loading="props.loading" @click="submit" />
    </div>
  </UCard>
</template>
