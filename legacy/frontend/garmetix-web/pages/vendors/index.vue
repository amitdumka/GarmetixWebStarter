<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'
const api = useGarmetixApi(); const auth = useAuth(); const workspace = useWorkspace(); const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated; const canEdit = auth.canEdit; const canDelete = auth.canDelete
const UButton = resolveComponent('UButton'); const UBadge = resolveComponent('UBadge')
const rows = ref<any[]>([]); const search = ref(''); const loading = ref(false); const loadError = ref(''); const formOpen = ref(false); const saving = ref(false)
const form = reactive<any>(emptyForm())
function emptyForm(){ return { id:'', companyId: workspace.companyId.value || '', name:'', address:'', city:'Dumka', zipCode:'814101', mobileNumber:'', email:'', gstin:'', pan:'', tan:'', active:true } }
const filteredRows = computed(()=> rows.value.filter((r:any)=>{ const t=search.value.trim().toLowerCase(); if(!t) return true; return [r.name,r.mobileNumber,r.gstin,r.city].some(v=>String(v||'').toLowerCase().includes(t)) }))
const columns: TableColumn<any>[] = [
 {accessorKey:'name', header:'Vendor'}, {accessorKey:'mobileNumber', header:'Mobile'}, {accessorKey:'gstin', header:'GSTIN'}, {accessorKey:'city', header:'City'}, {accessorKey:'balanceText', header:'Balance'},
 {accessorKey:'active', header:'Status', cell:({row})=>h(UBadge,{color:row.original.active?'success':'neutral',variant:'subtle'},()=>row.original.active?'Active':'Inactive')},
 {id:'actions', header:'', cell:({row})=>h('div',{class:'flex justify-end gap-1'},[
   h(UButton,{icon:'i-lucide-pencil',label:'Edit',size:'sm',variant:'ghost',disabled:!canEdit.value,onClick:()=>startEdit(row.original)}),
   h(UButton,{icon:'i-lucide-trash-2',label:'Delete',size:'sm',variant:'ghost',color:'error',disabled:!canDelete.value,onClick:()=>removeRow(row.original)})
 ])}
]
async function refresh(){ loading.value=true; loadError.value=''; try{ rows.value=(await api.list<any>('vendors')).map((r:any)=>({...r,balanceText:money(Number(r.balanceAmount||0))})) }catch(e:any){ loadError.value=e.message||'Vendor load failed'; feedback.failed('Vendor load failed',e)} finally{loading.value=false}}
function startCreate(){ Object.assign(form, emptyForm()); form.companyId=workspace.companyId.value || form.companyId; formOpen.value=true }
function startEdit(row:any){ Object.assign(form, {...emptyForm(), ...row, gstin: row.gstin || ''}); formOpen.value=true }
async function save(){ saving.value=true; try{ if(!form.companyId) throw new Error('Select company/workspace first.'); if(form.id) await api.update<any>('vendors',form.id,form); else await api.create<any>('vendors',form); feedback.saved('Vendor saved'); formOpen.value=false; await refresh()}catch(e:any){feedback.failed('Vendor save failed',e)}finally{saving.value=false}}
async function removeRow(row:any){ if(!confirm(`Delete or inactivate vendor ${row.name}?`)) return; try{ await api.remove('vendors',row.id); feedback.notify('Vendor removed'); await refresh()}catch(e:any){feedback.failed('Vendor delete failed',e)}}
function money(value:number){ return new Intl.NumberFormat('en-IN',{style:'currency',currency:'INR',maximumFractionDigits:2}).format(value||0)}
onMounted(()=>{ auth.restore(); refresh() })
</script>
<template>
<AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
<AppShell v-else title="Vendors" @refresh="refresh" @workspace-change="refresh">
<section class="planner-dashboard">
<UiModulePageHeader title="Vendors" description="Supplier/vendor master is now separated from CRM and kept under Purchase." icon="i-lucide-truck" primary-label="New Vendor" primary-icon="i-lucide-plus" @primary="startCreate"><template #actions><UBadge color="primary" variant="subtle">{{ filteredRows.length }} vendors</UBadge><UButton icon="i-lucide-plus" label="New Vendor" @click="startCreate" /></template></UiModulePageHeader>
<UiRegisterPanel title="Vendor Listing" description="Add, edit, inactivate or delete purchase vendors." :loading="loading" :error="loadError" :empty="filteredRows.length===0" empty-title="No vendors found" empty-description="Change search or add a vendor." empty-icon="i-lucide-truck" @retry="refresh"><template #actions><UiCrudToolbar v-model:search="search" search-placeholder="Search vendor, mobile, GSTIN" :loading="loading" create-label="New Vendor" @refresh="refresh" @create="startCreate" /></template><div class="planner-table-wrap"><UTable :data="filteredRows" :columns="columns" /></div></UiRegisterPanel>
<UiFormSlideover v-model:open="formOpen" :title="form.id?'Edit Vendor':'New Vendor'" description="Supplier details used in purchase inward and vendor payments." submit-label="Save Vendor" :loading="saving" @submit="save">
<div class="form-two-column"><UFormField label="Name" required><UInput v-model="form.name" required /></UFormField><UFormField label="Mobile"><UInput v-model="form.mobileNumber" /></UFormField></div>
<UFormField label="Address"><UTextarea v-model="form.address" /></UFormField>
<div class="form-three-column"><UFormField label="City"><UInput v-model="form.city" /></UFormField><UFormField label="Zip"><UInput v-model="form.zipCode" /></UFormField><UFormField label="Email"><UInput v-model="form.email" /></UFormField></div>
<div class="form-three-column"><UFormField label="GSTIN"><UInput v-model="form.gstin" /></UFormField><UFormField label="PAN"><UInput v-model="form.pan" /></UFormField><UFormField label="TAN"><UInput v-model="form.tan" /></UFormField></div>
<UCheckbox v-model="form.active" label="Active vendor" />
</UiFormSlideover>
</section></AppShell></template>
