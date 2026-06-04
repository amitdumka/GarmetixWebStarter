<script setup lang="ts">
import { Banknote, Pencil, Plus, Trash2 } from 'lucide-vue-next'

const api = useGarmetixApi()
const auth = useAuth()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const vouchers = ref<any[]>([])
const setupStatus = ref<any | null>(null)
const loading = ref(false)
const viewMode = ref<'list' | 'create' | 'edit'>('list')
const voucherMessage = ref('')
const searchText = ref('')

const voucherTypeOptions = [
  { value: 0, label: 'Payment' },
  { value: 1, label: 'Receipt' },
  { value: 2, label: 'Expense' }
]

const paymentModeOptions = [
  { value: 0, label: 'Cash' },
  { value: 1, label: 'Card' },
  { value: 2, label: 'UPI' },
  { value: 6, label: 'NEFT' },
  { value: 7, label: 'Cheque' }
]

const form = reactive<any>(emptyVoucher())

const filteredVouchers = computed(() => {
  const query = searchText.value.trim().toLowerCase()
  if (!query) {
    return vouchers.value
  }

  return vouchers.value.filter((voucher) => {
    return String(voucher.voucherNumber || '').toLowerCase().includes(query) ||
      String(voucher.partyName || '').toLowerCase().includes(query) ||
      String(voucher.particulars || '').toLowerCase().includes(query)
  })
})

function emptyVoucher() {
  return {
    id: '',
    voucherNumber: createVoucherNumber(),
    onDate: new Date().toISOString().slice(0, 10),
    voucherType: 0,
    partyName: '',
    particulars: '',
    amount: 0,
    remarks: '',
    slipNumber: '',
    paymentMode: 0,
    paymentDetails: '',
    isParty: false
  }
}

function createVoucherNumber() {
  const date = new Date()
  const stamp = date.toISOString().slice(0, 10).replaceAll('-', '')
  const suffix = String(Date.now()).slice(-4)
  return `V-${stamp}-${suffix}`
}

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, voucherRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('vouchers')
    ])

    companies.value = companyRows
    stores.value = storeRows
    vouchers.value = voucherRows
  } finally {
    loading.value = false
  }
}

function resetForm() {
  Object.assign(form, emptyVoucher())
}

function startCreate() {
  resetForm()
  voucherMessage.value = ''
  viewMode.value = 'create'
}

function startEdit(voucher: any) {
  Object.assign(form, {
    ...voucher,
    onDate: String(voucher.onDate || new Date().toISOString()).slice(0, 10),
    ledger: null,
    employee: null,
    party: null,
    bankAccount: null
  })
  voucherMessage.value = ''
  viewMode.value = 'edit'
}

function buildPayload() {
  const companyId = setupStatus.value?.companyId || companies.value[0]?.id
  const storeGroupId = setupStatus.value?.storeGroupId || stores.value[0]?.storeGroupId
  const storeId = setupStatus.value?.storeId || stores.value[0]?.id

  if (!companyId || !storeGroupId || !storeId) {
    throw new Error('Run quick setup before saving vouchers.')
  }

  return {
    ...form,
    voucherNumber: String(form.voucherNumber || '').trim(),
    onDate: new Date(form.onDate).toISOString(),
    voucherType: Number(form.voucherType),
    partyName: String(form.partyName || '').trim(),
    particulars: String(form.particulars || '').trim(),
    amount: Number(form.amount || 0),
    paymentMode: Number(form.paymentMode),
    companyId,
    storeGroupId,
    storeId
  }
}

async function saveVoucher() {
  voucherMessage.value = ''

  try {
    const payload = buildPayload()
    if (viewMode.value === 'edit' && form.id) {
      await api.update<any>('vouchers', form.id, payload)
      voucherMessage.value = 'Voucher updated.'
    } else {
      await api.create<any>('vouchers', payload)
      voucherMessage.value = 'Voucher saved.'
    }

    viewMode.value = 'list'
    await refresh()
  } catch (error: any) {
    voucherMessage.value = error?.data?.message || error?.message || 'Could not save voucher.'
  }
}

async function deleteVoucher(voucher: any) {
  const confirmed = window.confirm(`Delete voucher ${voucher.voucherNumber}?`)
  if (!confirmed) {
    return
  }

  await api.remove('vouchers', voucher.id)
  voucherMessage.value = 'Voucher deleted.'
  await refresh()
}

function voucherTypeLabel(value: number) {
  return voucherTypeOptions.find((item) => item.value === Number(value))?.label || 'Voucher'
}

function paymentModeLabel(value: number) {
  return paymentModeOptions.find((item) => item.value === Number(value))?.label || 'Other'
}

onMounted(async () => {
  auth.restore()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Vouchers"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="content">
      <section class="panel">
        <div class="panel-header">
          <h2 class="panel-title">Accounting Vouchers</h2>
          <div class="panel-actions">
            <span class="status" :class="loading ? 'warn' : 'ok'">{{ loading ? 'Loading' : `${vouchers.length} vouchers` }}</span>
            <button class="button secondary" type="button" @click="viewMode = 'list'">
              <Banknote :size="16" />
              List
            </button>
            <button class="button" type="button" @click="startCreate">
              <Plus :size="16" />
              New Voucher
            </button>
          </div>
        </div>

        <div v-if="viewMode === 'list'" class="panel-body">
          <div class="table-toolbar">
            <input v-model="searchText" class="search" aria-label="Search vouchers" placeholder="Search voucher, party, particulars" />
            <p v-if="voucherMessage" class="inline-message">{{ voucherMessage }}</p>
          </div>
          <table class="table">
            <thead>
              <tr>
                <th>Voucher</th>
                <th>Date</th>
                <th>Type</th>
                <th>Party</th>
                <th>Particulars</th>
                <th>Mode</th>
                <th>Amount</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="voucher in filteredVouchers" :key="voucher.id">
                <td>{{ voucher.voucherNumber }}</td>
                <td>{{ new Date(voucher.onDate).toLocaleDateString() }}</td>
                <td><span class="status ok">{{ voucherTypeLabel(voucher.voucherType) }}</span></td>
                <td>{{ voucher.partyName }}</td>
                <td>{{ voucher.particulars }}</td>
                <td>{{ paymentModeLabel(voucher.paymentMode) }}</td>
                <td>{{ Number(voucher.amount).toFixed(2) }}</td>
                <td>
                  <button class="button secondary" type="button" @click="startEdit(voucher)">
                    <Pencil :size="16" />
                    Edit
                  </button>
                  <button class="button danger-button" type="button" @click="deleteVoucher(voucher)">
                    <Trash2 :size="16" />
                    Delete
                  </button>
                </td>
              </tr>
              <tr v-if="filteredVouchers.length === 0">
                <td colspan="8">No vouchers</td>
              </tr>
            </tbody>
          </table>
        </div>

        <form v-else class="form-grid wide-form" @submit.prevent="saveVoucher">
          <div class="field">
            <label for="voucherNumber">Voucher number</label>
            <input id="voucherNumber" v-model="form.voucherNumber" required />
          </div>
          <div class="field">
            <label for="voucherDate">Date</label>
            <input id="voucherDate" v-model="form.onDate" required type="date" />
          </div>
          <div class="field">
            <label for="voucherType">Type</label>
            <select id="voucherType" v-model="form.voucherType">
              <option v-for="item in voucherTypeOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
            </select>
          </div>
          <div class="field">
            <label for="paymentMode">Payment mode</label>
            <select id="paymentMode" v-model="form.paymentMode">
              <option v-for="item in paymentModeOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
            </select>
          </div>
          <div class="field">
            <label for="partyName">Party</label>
            <input id="partyName" v-model="form.partyName" required />
          </div>
          <div class="field">
            <label for="amount">Amount</label>
            <input id="amount" v-model="form.amount" min="0" required type="number" />
          </div>
          <div class="field">
            <label for="particulars">Particulars</label>
            <input id="particulars" v-model="form.particulars" required />
          </div>
          <div class="field">
            <label for="paymentDetails">Payment details</label>
            <input id="paymentDetails" v-model="form.paymentDetails" />
          </div>
          <div class="field">
            <label for="slipNumber">Slip number</label>
            <input id="slipNumber" v-model="form.slipNumber" />
          </div>
          <div class="field">
            <label for="remarks">Remarks</label>
            <input id="remarks" v-model="form.remarks" />
          </div>
          <div class="form-actions">
            <button class="button secondary" type="button" @click="viewMode = 'list'">Cancel</button>
            <button class="button" type="submit">
              <Banknote :size="16" />
              Save Voucher
            </button>
          </div>
          <p v-if="voucherMessage" class="setup-message">{{ voucherMessage }}</p>
        </form>
      </section>
    </section>
  </AppShell>
</template>
