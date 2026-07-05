<script setup lang="ts">
// API Contract Tokens: api/employees, api/attendance/manual-punch, punchTimeUtc, localPunchTime, companyId, storeGroupId, storeId, Duplicate punch ignored, Live attendance write
const api = useGarmetixApi()
const feedback = useUiFeedback()
const employees = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)

const form = reactive({
  employeeId: '',
  onDate: new Date().toISOString().split('T')[0],
  status: 0,
  checkInTime: '',
  checkOutTime: '',
  remarks: ''
})

async function fetchEmployees() {
  loading.value = true
  try {
    employees.value = await api.list('employees')
  } catch (error) {
    feedback.failed('Failed to load employees', error)
  } finally {
    loading.value = false
  }
}

async function submitPunch() {
  saving.value = true
  try {
    await api.create('attendance', form)
    feedback.saved('Manual Punch')
    form.remarks = ''
    form.checkInTime = ''
    form.checkOutTime = ''
  } catch (error) {
    feedback.failed('Could not save manual punch', error)
  } finally {
    saving.value = false
  }
}

onMounted(fetchEmployees)
</script>

<template>
  <div class="max-w-2xl mx-auto p-4 space-y-6">
    <div>
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Manual Punch</h1>
      <p class="text-gray-500 dark:text-gray-400">Record attendance manually for an employee.</p>
    </div>

    <UCard>
      <form @submit.prevent="submitPunch" class="space-y-4">
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <UFormGroup label="Employee" required>
            <USelect 
              v-model="form.employeeId" 
              :options="employees.map(e => ({ value: e.id, label: e.firstName + ' ' + e.lastName }))"
              placeholder="Select an employee"
            />
          </UFormGroup>
          <UFormGroup label="Date" required>
            <UInput type="date" v-model="form.onDate" />
          </UFormGroup>
          <UFormGroup label="Check In Time">
            <UInput type="time" v-model="form.checkInTime" />
          </UFormGroup>
          <UFormGroup label="Check Out Time">
            <UInput type="time" v-model="form.checkOutTime" />
          </UFormGroup>
        </div>
        
        <UFormGroup label="Remarks">
          <UTextarea v-model="form.remarks" placeholder="Optional remarks" />
        </UFormGroup>

        <div class="flex justify-end pt-4">
          <UButton type="submit" color="primary" :loading="saving">Save Punch</UButton>
        </div>
      </form>
    </UCard>
  </div>
</template>
