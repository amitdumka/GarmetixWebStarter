<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()

const loading = ref(false)
const saving = ref(false)
const loadError = ref('')
const activeTab = ref<'dashboard' | 'orders' | 'services'>('dashboard')
const companies = ref<any[]>([])
const stores = ref<any[]>([])
const customers = ref<any[]>([])
const vendors = ref<any[]>([])
const serviceItems = ref<any[]>([])
const orders = ref<any[]>([])
const dashboard = ref<any | null>(null)
const selectedOrder = ref<any | null>(null)

const orderTypeOptions = [
  { label: 'Stitching', value: 0 },
  { label: 'Alteration', value: 1 }
]
const serviceCategoryOptions = [
  { label: 'Stitching', value: 0 },
  { label: 'Alteration', value: 1 },
  { label: 'Measurement', value: 2 },
  { label: 'Finishing', value: 3 },
  { label: 'Other', value: 4 }
]
const responsibilityOptions = [
  { label: 'Customer chargeable', value: 0 },
  { label: 'In-house expense', value: 1 },
  { label: 'Complimentary', value: 2 }
]
const paymentModeOptions = [
  { label: 'Cash', value: 0 },
  { label: 'Card', value: 1 },
  { label: 'UPI', value: 2 },
  { label: 'Wallet', value: 3 },
  { label: 'IMPS', value: 4 },
  { label: 'RTGS', value: 5 },
  { label: 'NEFT', value: 6 },
  { label: 'Cheque', value: 7 },
  { label: 'Other', value: 14 }
]

const form = reactive<any>(emptyOrder())
const serviceForm = reactive<any>(emptyService())
const paymentForm = reactive<any>({ onDate: localDateValue(), amount: 0, paymentMode: 0, bankAccountId: null, referenceNumber: '', remarks: '' })

const companyOptions = computed(() => companies.value.map((item: any) => ({ label: item.name, value: item.id })))
const storeOptions = computed(() => stores.value.map((item: any) => ({ label: `${item.name || item.storeName || item.storeCode}`, value: item.id })))
const customerOptions = computed(() => customers.value.map((item: any) => ({ label: `${item.name} | ${item.mobileNumber || ''}`, value: item.id })))
const vendorOptions = computed(() => vendors.value.map((item: any) => ({ label: `${item.name} (${item.vendorType})`, value: item.id })))
const serviceOptions = computed(() => serviceItems.value.map((item: any) => ({ label: `${item.name} · ₹${item.defaultCustomerRate}`, value: item.id })))
const totals = computed(() => form.lines.reduce((total: any, line: any) => {
  const qty = Number(line.quantity || 0)
  const charge = Math.max(0, qty * Number(line.customerRate || 0) - Number(line.discountAmount || 0))
  const cost = Math.max(0, qty * Number(line.vendorRate || 0))
  total.charge += charge
  total.cost += cost
  total.inHouse += Number(line.costResponsibility) === 1 ? cost : 0
  total.profit += charge - cost
  return total
}, { charge: 0, cost: 0, inHouse: 0, profit: 0 }))

async function loadAll() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    const [companyList, storeList, customerList, vendorList, serviceList, orderList, dashboardData] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('customers'),
      api.get<any[]>('tailoring/vendors'),
      api.get<any[]>('tailoring/service-items'),
      api.get<any[]>('tailoring/orders'),
      api.get<any>('tailoring/dashboard')
    ])
    companies.value = companyList
    stores.value = storeList
    customers.value = customerList
    vendors.value = vendorList
    serviceItems.value = serviceList
    orders.value = orderList
    dashboard.value = dashboardData
    hydrateDefaults()
  } catch (error: any) {
    loadError.value = error?.data?.message || error?.message || 'Could not load tailoring workspace.'
  } finally {
    loading.value = false
  }
}

function hydrateDefaults() {
  if (!form.companyId && workspace.companyId.value) form.companyId = workspace.companyId.value
  if (!form.storeId && workspace.storeId.value) form.storeId = workspace.storeId.value
  if (!form.storeGroupId && workspace.storeGroupId.value) form.storeGroupId = workspace.storeGroupId.value
  if (!form.companyId && companies.value[0]) form.companyId = companies.value[0].id
  if (!form.storeId && stores.value[0]) {
    form.storeId = stores.value[0].id
    form.storeGroupId = stores.value[0].storeGroupId || form.storeGroupId
    form.companyId = stores.value[0].companyId || form.companyId
  }
  if (!serviceForm.companyId) serviceForm.companyId = form.companyId
  if (!serviceForm.storeGroupId) serviceForm.storeGroupId = form.storeGroupId
  if (!serviceForm.storeId) serviceForm.storeId = form.storeId
}

function emptyOrder() {
  return {
    companyId: '',
    storeGroupId: '',
    storeId: '',
    orderType: 0,
    customerId: '',
    vendorId: null,
    sourceInvoiceId: null,
    sourceInvoiceItemId: null,
    sourceProductId: null,
    sourceProductName: '',
    sourceBarcode: '',
    expectedDeliveryDate: localDateValue(new Date(Date.now() + 3 * 86400000)),
    measurementsJson: '',
    customerInstructions: '',
    internalRemarks: '',
    lines: [emptyLine()]
  }
}

function emptyLine() {
  return {
    serviceItemId: null,
    serviceName: '',
    category: 0,
    garmentName: '',
    barcode: '',
    quantity: 1,
    customerRate: 0,
    vendorRate: 0,
    discountAmount: 0,
    costResponsibility: 0,
    expectedDeliveryDate: localDateValue(new Date(Date.now() + 3 * 86400000)),
    measurementsJson: '',
    instructions: '',
    vendorRemarks: ''
  }
}

function emptyService() {
  return {
    companyId: '',
    storeGroupId: '',
    storeId: '',
    serviceCode: '',
    name: '',
    category: 0,
    defaultCustomerRate: 0,
    defaultVendorRate: 0,
    taxRate: 0,
    hsnCode: '',
    productId: null,
    active: true,
    remarks: ''
  }
}

function addLine() { form.lines.push(emptyLine()) }
function removeLine(index: number) { if (form.lines.length > 1) form.lines.splice(index, 1) }
function applyService(line: any) {
  const item = serviceItems.value.find((service: any) => service.id === line.serviceItemId)
  if (!item) return
  line.serviceName = item.name
  line.category = item.category
  line.customerRate = item.defaultCustomerRate
  line.vendorRate = item.defaultVendorRate
}

async function saveOrder() {
  saving.value = true
  try {
    const payload = { ...form, lines: form.lines.map((line: any) => ({ ...line, quantity: Number(line.quantity || 0), customerRate: Number(line.customerRate || 0), vendorRate: Number(line.vendorRate || 0), discountAmount: Number(line.discountAmount || 0) })) }
    await api.create<any>('tailoring/orders', payload)
    Object.assign(form, emptyOrder())
    hydrateDefaults()
    await loadAll()
    activeTab.value = 'orders'
    feedback.showSuccess('Tailoring order saved.')
  } catch (error: any) {
    feedback.showError(error?.data?.message || error?.message || 'Could not save tailoring order.')
  } finally {
    saving.value = false
  }
}

async function saveService() {
  saving.value = true
  try {
    await api.create<any>('tailoring/service-items', { ...serviceForm, defaultCustomerRate: Number(serviceForm.defaultCustomerRate || 0), defaultVendorRate: Number(serviceForm.defaultVendorRate || 0), taxRate: Number(serviceForm.taxRate || 0) })
    Object.assign(serviceForm, emptyService())
    hydrateDefaults()
    await loadAll()
    feedback.showSuccess('Tailoring service item saved.')
  } catch (error: any) {
    feedback.showError(error?.data?.message || error?.message || 'Could not save service item.')
  } finally {
    saving.value = false
  }
}

async function openOrder(order: any) {
  selectedOrder.value = await api.get<any>(`tailoring/orders/${order.id}`)
}

async function deliver(order: any) {
  await api.create<any>(`tailoring/orders/${order.id}/deliver`, { deliveredAt: localDateValue(), remarks: 'Delivered to customer' })
  await loadAll()
  feedback.showSuccess('Order marked delivered.')
}

async function convertToInvoice(order: any) {
  await api.create<any>(`tailoring/orders/${order.id}/convert-to-service-invoice`, { invoiceDate: localDateValue(), additionalPaidAmount: 0, additionalPaymentMode: 0, bankAccountId: null, referenceNumber: '', remarks: 'Converted from tailoring order' })
  await loadAll()
  feedback.showSuccess('Service invoice created.')
}

async function receivePayment(order: any) {
  await api.create<any>(`tailoring/orders/${order.id}/receive-payment`, { ...paymentForm, amount: Number(paymentForm.amount || 0) })
  await loadAll()
  feedback.showSuccess('Customer receipt saved.')
}

function localDateValue(date = new Date()) {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000)
  return local.toISOString().slice(0, 10)
}
function money(value: any) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(value || 0)) }
function dateText(value: any) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }

watch(() => form.storeId, () => {
  const store = stores.value.find((item: any) => item.id === form.storeId)
  if (store) {
    form.companyId = store.companyId || form.companyId
    form.storeGroupId = store.storeGroupId || form.storeGroupId
    serviceForm.companyId = form.companyId
    serviceForm.storeGroupId = form.storeGroupId
    serviceForm.storeId = form.storeId
  }
})

onMounted(loadAll)
</script>

<template>
  <AppShell title="Tailoring & Alteration" :companies="companies" :stores="stores" @refresh="loadAll" @workspace-change="loadAll">
    <div class="space-y-6">
      <PageHero
        title="Tailoring & Alteration"
        subtitle="Manage stitching orders, readymade garment alteration, vendor work, delivery schedule, service invoice conversion, customer receipts and vendor payments."
        icon="i-lucide-scissors"
      />

      <UAlert v-if="loadError" color="error" variant="soft" :title="loadError" />

      <div class="flex flex-wrap gap-2">
        <UButton :variant="activeTab === 'dashboard' ? 'solid' : 'soft'" icon="i-lucide-layout-dashboard" @click="activeTab = 'dashboard'">Dashboard</UButton>
        <UButton :variant="activeTab === 'orders' ? 'solid' : 'soft'" icon="i-lucide-list-checks" @click="activeTab = 'orders'">Orders</UButton>
        <UButton :variant="activeTab === 'services' ? 'solid' : 'soft'" icon="i-lucide-shirt" @click="activeTab = 'services'">Service items</UButton>
      </div>

      <section v-if="activeTab === 'dashboard'" class="space-y-4">
        <div class="grid gap-4 md:grid-cols-4">
          <UCard><p class="text-sm text-muted">Pending</p><p class="text-2xl font-bold">{{ dashboard?.pendingOrders || 0 }}</p></UCard>
          <UCard><p class="text-sm text-muted">Due today</p><p class="text-2xl font-bold">{{ dashboard?.dueToday || 0 }}</p></UCard>
          <UCard><p class="text-sm text-muted">Overdue</p><p class="text-2xl font-bold text-error">{{ dashboard?.overdue || 0 }}</p></UCard>
          <UCard><p class="text-sm text-muted">In-house expense impact</p><p class="text-2xl font-bold">{{ money(dashboard?.inHouseExpenseImpact) }}</p></UCard>
        </div>
        <div class="grid gap-4 lg:grid-cols-2">
          <UCard>
            <template #header>Upcoming delivery schedule</template>
            <div v-for="order in dashboard?.upcomingDeliveries || []" :key="order.id" class="flex items-center justify-between border-b border-default py-2 last:border-0">
              <div><p class="font-medium">{{ order.orderNumber }} · {{ order.customerName }}</p><p class="text-xs text-muted">{{ dateText(order.expectedDeliveryDate) }} · {{ order.orderType }}</p></div>
              <UBadge variant="subtle">{{ order.status }}</UBadge>
            </div>
          </UCard>
          <UCard>
            <template #header>Overdue tailoring / alteration</template>
            <div v-for="order in dashboard?.overdueOrders || []" :key="order.id" class="flex items-center justify-between border-b border-default py-2 last:border-0">
              <div><p class="font-medium">{{ order.orderNumber }} · {{ order.customerName }}</p><p class="text-xs text-muted">{{ dateText(order.expectedDeliveryDate) }} · {{ order.vendorName || 'No vendor' }}</p></div>
              <UBadge color="error" variant="subtle">Overdue</UBadge>
            </div>
          </UCard>
        </div>
      </section>

      <section v-if="activeTab === 'orders'" class="grid gap-6 xl:grid-cols-[1fr_1.2fr]">
        <UCard>
          <template #header>New stitching / alteration order</template>
          <div class="grid gap-3">
            <UFormField label="Store"><USelect v-model="form.storeId" :items="storeOptions" /></UFormField>
            <UFormField label="Order type"><USelect v-model="form.orderType" :items="orderTypeOptions" /></UFormField>
            <UFormField label="Customer"><USelect v-model="form.customerId" :items="customerOptions" /></UFormField>
            <UFormField label="Tailoring vendor"><USelect v-model="form.vendorId" :items="vendorOptions" placeholder="Optional / in-house" /></UFormField>
            <UFormField label="Expected delivery"><UInput v-model="form.expectedDeliveryDate" type="date" /></UFormField>
            <UFormField label="Source product / garment"><UInput v-model="form.sourceProductName" placeholder="Readymade garment or fabric name" /></UFormField>
            <UFormField label="Barcode"><UInput v-model="form.sourceBarcode" /></UFormField>
            <UFormField label="Instructions"><UTextarea v-model="form.customerInstructions" /></UFormField>
          </div>

          <div class="mt-5 space-y-3">
            <div v-for="(line, index) in form.lines" :key="index" class="rounded-lg border border-default p-3">
              <div class="grid gap-3 md:grid-cols-2">
                <UFormField label="Service item"><USelect v-model="line.serviceItemId" :items="serviceOptions" @update:model-value="applyService(line)" /></UFormField>
                <UFormField label="Service name"><UInput v-model="line.serviceName" /></UFormField>
                <UFormField label="Category"><USelect v-model="line.category" :items="serviceCategoryOptions" /></UFormField>
                <UFormField label="Responsibility"><USelect v-model="line.costResponsibility" :items="responsibilityOptions" /></UFormField>
                <UFormField label="Qty"><UInput v-model="line.quantity" type="number" /></UFormField>
                <UFormField label="Customer rate"><UInput v-model="line.customerRate" type="number" /></UFormField>
                <UFormField label="Vendor rate"><UInput v-model="line.vendorRate" type="number" /></UFormField>
                <UFormField label="Discount"><UInput v-model="line.discountAmount" type="number" /></UFormField>
                <UFormField label="Garment"><UInput v-model="line.garmentName" /></UFormField>
                <UFormField label="Line delivery"><UInput v-model="line.expectedDeliveryDate" type="date" /></UFormField>
              </div>
              <UButton class="mt-3" size="xs" color="error" variant="soft" @click="removeLine(index)">Remove line</UButton>
            </div>
            <UButton variant="soft" icon="i-lucide-plus" @click="addLine">Add service line</UButton>
          </div>

          <div class="mt-4 grid gap-2 rounded-lg bg-muted p-3 text-sm md:grid-cols-4">
            <span>Customer charge: <b>{{ money(totals.charge) }}</b></span>
            <span>Vendor cost: <b>{{ money(totals.cost) }}</b></span>
            <span>In-house expense: <b>{{ money(totals.inHouse) }}</b></span>
            <span>Profit impact: <b>{{ money(totals.profit) }}</b></span>
          </div>
          <UButton class="mt-4" icon="i-lucide-save" :loading="saving" @click="saveOrder">Save order</UButton>
        </UCard>

        <UCard>
          <template #header>Order history and status</template>
          <div class="space-y-3">
            <div v-for="order in orders" :key="order.id" class="rounded-lg border border-default p-3">
              <div class="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <p class="font-semibold">{{ order.orderNumber }} · {{ order.customerName }}</p>
                  <p class="text-xs text-muted">{{ order.orderType }} · Delivery {{ dateText(order.expectedDeliveryDate) }} · Vendor {{ order.vendorName || 'In-house / not assigned' }}</p>
                </div>
                <UBadge variant="subtle">{{ order.status }}</UBadge>
              </div>
              <div class="mt-2 grid gap-2 text-sm md:grid-cols-4">
                <span>Charge {{ money(order.customerChargeAmount) }}</span>
                <span>Received {{ money(order.customerReceivedAmount) }}</span>
                <span>Vendor due {{ money(order.vendorBalanceAmount) }}</span>
                <span>Profit {{ money(order.profitImpactAmount) }}</span>
              </div>
              <div class="mt-3 flex flex-wrap gap-2">
                <UButton size="xs" variant="soft" @click="openOrder(order)">History</UButton>
                <UButton size="xs" variant="soft" @click="deliver(order)">Deliver</UButton>
                <UButton size="xs" variant="soft" @click="convertToInvoice(order)">Convert to service invoice</UButton>
                <UButton size="xs" variant="soft" @click="receivePayment(order)">Receive payment</UButton>
              </div>
            </div>
          </div>
        </UCard>
      </section>

      <section v-if="activeTab === 'services'" class="grid gap-6 lg:grid-cols-[0.8fr_1.2fr]">
        <UCard>
          <template #header>Tailoring service item</template>
          <div class="grid gap-3">
            <UFormField label="Store"><USelect v-model="serviceForm.storeId" :items="storeOptions" /></UFormField>
            <UFormField label="Code"><UInput v-model="serviceForm.serviceCode" placeholder="STITCH-SHIRT" /></UFormField>
            <UFormField label="Name"><UInput v-model="serviceForm.name" placeholder="Shirt stitching" /></UFormField>
            <UFormField label="Category"><USelect v-model="serviceForm.category" :items="serviceCategoryOptions" /></UFormField>
            <UFormField label="Default customer rate"><UInput v-model="serviceForm.defaultCustomerRate" type="number" /></UFormField>
            <UFormField label="Default vendor rate"><UInput v-model="serviceForm.defaultVendorRate" type="number" /></UFormField>
            <UFormField label="Tax rate"><UInput v-model="serviceForm.taxRate" type="number" /></UFormField>
            <UButton icon="i-lucide-save" :loading="saving" @click="saveService">Save service item</UButton>
          </div>
        </UCard>
        <UCard>
          <template #header>Available tailoring / alteration services</template>
          <div class="space-y-2">
            <div v-for="item in serviceItems" :key="item.id" class="flex items-center justify-between border-b border-default py-2 last:border-0">
              <div><p class="font-medium">{{ item.name }}</p><p class="text-xs text-muted">{{ item.serviceCode }} · Vendor {{ money(item.defaultVendorRate) }}</p></div>
              <p class="font-semibold">{{ money(item.defaultCustomerRate) }}</p>
            </div>
          </div>
        </UCard>
      </section>

      <USlideover :open="!!selectedOrder" @update:open="(value) => { if (!value) selectedOrder = null }">
        <template #content>
          <div class="space-y-4 p-4">
            <h3 class="text-lg font-semibold">{{ selectedOrder?.order?.orderNumber }} history</h3>
            <div v-for="entry in selectedOrder?.history || []" :key="entry.id" class="rounded border border-default p-3 text-sm">
              <p class="font-medium">{{ entry.action }}</p>
              <p class="text-muted">{{ dateText(entry.eventDate) }} · {{ entry.actor || 'System' }}</p>
              <p>{{ entry.remarks }}</p>
            </div>
          </div>
        </template>
      </USlideover>
    </div>
  </AppShell>
</template>
