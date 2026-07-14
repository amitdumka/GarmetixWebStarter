<script setup lang="ts">
const attendance = useAttendance(); const feedback = useUiFeedback(); const loading = ref(false); const modelDate = ref(new Date().toISOString().slice(0,10)); const today = ref<any|null>(null)
async function refresh(){ loading.value=true; try{ today.value=await attendance.today(modelDate.value) }catch(e:any){ feedback.fromError('Today attendance failed',e) } finally{ loading.value=false } }
onMounted(refresh)
</script>
<template><AppShell title="Today Attendance" @refresh="refresh"><section class="space-y-5"><UiModulePageHeader title="Today Attendance" description="Daily employee status from punches and Stage 9B rule engine." icon="i-lucide-calendar-days"><template #actions><UInput v-model="modelDate" type="date" @change="refresh"/><UButton label="Refresh" :loading="loading" @click="refresh"/></template></UiModulePageHeader><AttendanceTodayTable :rows="today?.rows || []" /></section></AppShell></template>
