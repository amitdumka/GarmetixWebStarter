<script setup lang="ts">
const attendance=useAttendance(); const feedback=useUiFeedback(); const loading=ref(false); const rows=ref<any[]>([])
async function refresh(){ loading.value=true; try{ rows.value=await attendance.regularization() }catch(e:any){ feedback.fromError('Regularization refresh failed',e) } finally{ loading.value=false } }
async function approve(id:string){ await attendance.approveRegularization(id,'Approved from Stage 9B page'); await refresh() }
async function reject(id:string){ await attendance.rejectRegularization(id,'Rejected from Stage 9B page'); await refresh() }
onMounted(refresh)
</script>
<template><AppShell title="Attendance Regularization" @refresh="refresh"><section class="space-y-5"><UiModulePageHeader title="Regularization Requests" description="Manager approval queue for missed punches and corrections." icon="i-lucide-list-checks"><template #actions><UButton label="Refresh" :loading="loading" @click="refresh"/></template></UiModulePageHeader><AttendanceRegularizationList :rows="rows" @approve="approve" @reject="reject"/></section></AppShell></template>
