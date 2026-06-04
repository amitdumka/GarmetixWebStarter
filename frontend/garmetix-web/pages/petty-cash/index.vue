<script setup lang="ts">
import { CircleDollarSign, Pencil, Plus, Trash2 } from 'lucide-vue-next'

const api = useGarmetixApi()
const auth = useAuth()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const sheets = ref<any[]>([])
const loading = ref(false)
const viewMode = ref<'list' | 'create' | 'edit'>('list')
const pettyCashMessage = ref('')
const searchText = ref('')

const form = reactive<any>(emptySheet())

const filteredSheets = computed(() => {
  const query = searchText.value.trim().toLowerCase()
  if (!query) {
    return sheets.value
  }

  return sheets.value.filter((sheet) => {
    return storeName(sheet.storeId).toLowerCase().includes(query) ||
      String(sheet.createdBy || '').toLowerCase().includes(query) ||
      String(sheet.onDate || '').toLowerCase().includes(query)
  })
})

const totalIn = computed(() => {
  return Number(form.openingBalance || 0) +
    Number(form.sales || 0) +
    Number(form.receipts || 0) +
    Number(form.dueReceipts || 0) +
    Number(form.bankWithdrawal || 0)
})

const totalOut = computed(() => {
  return Number(form.expenses || 0) +
    Number(form.payments || 0) +
    Number(form.customerDue || 0) +
    Number(form.bankDeposit || 0) +
    Number(form.nonCashSale || 0)
})

const calculatedCash = computed(() => totalIn.value - totalOut.value)

function emptySheet() {
  return {
    id: '',
    storeId: '',
    onDate: new Date().toISOString().slice(0, 10),
    openingBalance: 0,
    sales: 0,
    receipts: 0,
    dueReceipts: 0,
    bankWithdrawal: 0,
    expenses: 0,
    payments: 0,
    customerDue: 0,
    bankDeposit: 0,
    nonCashSale: 0,
    cashInHand: 0,
    createdBy: 'AutoAdmin'
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    const [companyRows, storeRows, sheetRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('petty-cash-sheets')
    ])

    companies.value = companyRows
    stores.value = storeRows
    sheets.value = sheetRows.sort((a, b) => String(b.onDate).localeCompare(String(a.onDate)))
  } finally {
    loading.value = false
  }
}

function resetForm() {
  Object.assign(form, emptySheet())
  form.storeId = stores.value[0]?.id || ''
}

function startCreate() {
  resetForm()
  pettyCashMessage.value = ''
  viewMode.value = 'create'
}

function startEdit(sheet: any) {
  Object.assign(form, {
    ...sheet,
    onDate: String(sheet.onDate || new Date().toISOString()).slice(0, 10)
  })
  pettyCashMessage.value = ''
  viewMode.value = 'edit'
}

function buildPayload() {
  if (!form.storeId) {
    throw new Error('Select store before saving petty cash.')
  }

  return {
    ...form,
    storeId: form.storeId,
    onDate: new Date(form.onDate).toISOString(),
    openingBalance: Number(form.openingBalance || 0),
    sales: Number(form.sales || 0),
    receipts: Number(form.receipts || 0),
    dueReceipts: Number(form.dueReceipts || 0),
    bankWithdrawal: Number(form.bankWithdrawal || 0),
    expenses: Number(form.expenses || 0),
    payments: Number(form.payments || 0),
    customerDue: Number(form.customerDue || 0),
    bankDeposit: Number(form.bankDeposit || 0),
    nonCashSale: Number(form.nonCashSale || 0),
    cashInHand: Number(form.cashInHand || calculatedCash.value),
    createdBy: String(form.createdBy || 'AutoAdmin').trim()
  }
}

async function saveSheet() {
  pettyCashMessage.value = ''

  try {
    const payload = buildPayload()
    if (viewMode.value === 'edit' && form.id) {
      await api.update<any>('petty-cash-sheets', form.id, payload)
      pettyCashMessage.value = 'Petty cash sheet updated.'
    } else {
      await api.create<any>('petty-cash-sheets', payload)
      pettyCashMessage.value = 'Petty cash sheet saved.'
    }

    viewMode.value = 'list'
    await refresh()
  } catch (error: any) {
    pettyCashMessage.value = error?.data?.message || error?.message || 'Could not save petty cash sheet.'
  }
}

async function deleteSheet(sheet: any) {
  const confirmed = window.confirm(`Delete petty cash sheet for ${new Date(sheet.onDate).toLocaleDateString()}?`)
  if (!confirmed) {
    return
  }

  await api.remove('petty-cash-sheets', sheet.id)
  pettyCashMessage.value = 'Petty cash sheet deleted.'
  await refresh()
}

function storeName(storeId: string) {
  return stores.value.find((item) => item.id === storeId)?.name || 'Store'
}

function sheetCash(sheet: any) {
  return Number(sheet.cashInHand || 0)
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
    title="Petty Cash"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="content">
      <section class="panel">
        <div class="panel-header">
          <h2 class="panel-title">Daily Cash Sheets</h2>
          <div class="panel-actions">
            <span class="status" :class="loading ? 'warn' : 'ok'">{{ loading ? 'Loading' : `${sheets.length} sheets` }}</span>
            <button class="button secondary" type="button" @click="viewMode = 'list'">
              <CircleDollarSign :size="16" />
              List
            </button>
            <button class="button" type="button" @click="startCreate">
              <Plus :size="16" />
              New Sheet
            </button>
          </div>
        </div>

        <div v-if="viewMode === 'list'" class="panel-body">
          <div class="table-toolbar">
            <input v-model="searchText" class="search" aria-label="Search petty cash sheets" placeholder="Search store or date" />
            <p v-if="pettyCashMessage" class="inline-message">{{ pettyCashMessage }}</p>
          </div>
          <table class="table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Store</th>
                <th>Opening</th>
                <th>Sales</th>
                <th>Receipts</th>
                <th>Expenses</th>
                <th>Payments</th>
                <th>Cash In Hand</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="sheet in filteredSheets" :key="sheet.id">
                <td>{{ new Date(sheet.onDate).toLocaleDateString() }}</td>
                <td>{{ storeName(sheet.storeId) }}</td>
                <td>{{ Number(sheet.openingBalance).toFixed(2) }}</td>
                <td>{{ Number(sheet.sales).toFixed(2) }}</td>
                <td>{{ Number(sheet.receipts).toFixed(2) }}</td>
                <td>{{ Number(sheet.expenses).toFixed(2) }}</td>
                <td>{{ Number(sheet.payments).toFixed(2) }}</td>
                <td><span class="status" :class="sheetCash(sheet) >= 0 ? 'ok' : 'danger'">{{ sheetCash(sheet).toFixed(2) }}</span></td>
                <td>
                  <button class="button secondary" type="button" @click="startEdit(sheet)">
                    <Pencil :size="16" />
                    Edit
                  </button>
                  <button class="button danger-button" type="button" @click="deleteSheet(sheet)">
                    <Trash2 :size="16" />
                    Delete
                  </button>
                </td>
              </tr>
              <tr v-if="filteredSheets.length === 0">
                <td colspan="9">No petty cash sheets</td>
              </tr>
            </tbody>
          </table>
        </div>

        <form v-else class="form-grid wide-form" @submit.prevent="saveSheet">
          <div class="field">
            <label for="cashStore">Store</label>
            <select id="cashStore" v-model="form.storeId" required>
              <option value="">Select store</option>
              <option v-for="store in stores" :key="store.id" :value="store.id">{{ store.name }}</option>
            </select>
          </div>
          <div class="field">
            <label for="cashDate">Date</label>
            <input id="cashDate" v-model="form.onDate" required type="date" />
          </div>
          <div class="field">
            <label for="openingBalance">Opening balance</label>
            <input id="openingBalance" v-model="form.openingBalance" type="number" />
          </div>
          <div class="field">
            <label for="sales">Cash sales</label>
            <input id="sales" v-model="form.sales" type="number" />
          </div>
          <div class="field">
            <label for="receipts">Receipts</label>
            <input id="receipts" v-model="form.receipts" type="number" />
          </div>
          <div class="field">
            <label for="dueReceipts">Due receipts</label>
            <input id="dueReceipts" v-model="form.dueReceipts" type="number" />
          </div>
          <div class="field">
            <label for="bankWithdrawal">Bank withdrawal</label>
            <input id="bankWithdrawal" v-model="form.bankWithdrawal" type="number" />
          </div>
          <div class="field">
            <label for="expenses">Expenses</label>
            <input id="expenses" v-model="form.expenses" type="number" />
          </div>
          <div class="field">
            <label for="payments">Payments</label>
            <input id="payments" v-model="form.payments" type="number" />
          </div>
          <div class="field">
            <label for="customerDue">Customer due</label>
            <input id="customerDue" v-model="form.customerDue" type="number" />
          </div>
          <div class="field">
            <label for="bankDeposit">Bank deposit</label>
            <input id="bankDeposit" v-model="form.bankDeposit" type="number" />
          </div>
          <div class="field">
            <label for="nonCashSale">Non-cash sale</label>
            <input id="nonCashSale" v-model="form.nonCashSale" type="number" />
          </div>
          <div class="field">
            <label for="cashInHand">Cash in hand</label>
            <input id="cashInHand" v-model="form.cashInHand" type="number" />
          </div>
          <div class="payroll-summary">
            <span>Total in</span><strong>{{ totalIn.toFixed(2) }}</strong>
            <span>Total out</span><strong>{{ totalOut.toFixed(2) }}</strong>
            <span>Calculated cash</span><strong>{{ calculatedCash.toFixed(2) }}</strong>
          </div>
          <div class="form-actions">
            <button class="button secondary" type="button" @click="viewMode = 'list'">Cancel</button>
            <button class="button" type="submit">
              <CircleDollarSign :size="16" />
              Save Sheet
            </button>
          </div>
          <p v-if="pettyCashMessage" class="setup-message">{{ pettyCashMessage }}</p>
        </form>
      </section>
    </section>
  </AppShell>
</template>
