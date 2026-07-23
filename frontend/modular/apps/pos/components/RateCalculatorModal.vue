<template>
  <UModal v-model:open="openModel" title="Basic Rate Calculator" :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-lg' }">
    <template #body>
      <div class="grid gap-4">
        <div class="grid grid-cols-2 gap-3">
          <UFormField label="MRP (₹)" name="mrp">
            <UInput v-model.number="mrp" type="number" min="0" step="0.01" size="lg" placeholder="0.00" autofocus class="w-full" />
          </UFormField>
          <UFormField label="Type" name="productType">
            <USelect v-model="productType" :items="typeItems" size="lg" class="w-full" />
          </UFormField>
        </div>

        <UFormField v-if="productType === 'fabric'" label="Tax Rate % (editable - no fixed slab for Fabric)" name="fabricTaxRate">
          <UInput v-model.number="fabricTaxRate" type="number" min="0" max="100" step="0.01" class="w-32" />
        </UFormField>
        <UAlert
          v-else
          color="neutral"
          variant="subtle"
          icon="i-lucide-info"
          description="Apparel GST slab: 5% when basic price is up to ₹2,499, 18% above ₹2,499 (same threshold rule used by Sale Review). Rate is derived from MRP and may be approximate right at the boundary - confirm with your accountant for edge cases."
        />

        <div class="grid grid-cols-2 gap-3">
          <UFormField label="Discount %" name="discountPercent">
            <UInput v-model.number="discountPercent" type="number" min="0" max="100" step="0.01" placeholder="0" class="w-full" @input="discountMode = 'percent'" />
          </UFormField>
          <UFormField label="Discount Amount (₹)" name="discountAmount">
            <UInput v-model.number="discountAmount" type="number" min="0" step="0.01" placeholder="0.00" class="w-full" @input="discountMode = 'amount'" />
          </UFormField>
        </div>

        <div class="rounded-lg border border-default p-4 grid gap-2 text-sm">
          <div class="flex justify-between"><span class="text-muted">Applied Tax Rate</span><span class="font-semibold">{{ appliedRate }}%</span></div>
          <div class="flex justify-between"><span class="text-muted">Basic Price</span><span>{{ money(basicBeforeDiscount) }}</span></div>
          <div class="flex justify-between"><span class="text-muted">Tax Amount</span><span>{{ money(taxBeforeDiscount) }}</span></div>
          <hr class="border-default">
          <div class="flex justify-between"><span class="text-muted">Discount on Basic Price</span><span class="text-error">- {{ money(effectiveDiscountAmount) }}</span></div>
          <div class="flex justify-between font-semibold"><span>Basic Price After Discount</span><span>{{ money(adjustedBasic) }}</span></div>
          <div class="flex justify-between font-semibold"><span>Tax Amount After Discount</span><span>{{ money(adjustedTax) }}</span></div>
          <hr class="border-default">
          <div class="flex justify-between text-base font-bold"><span>Net Selling Price</span><span>{{ money(netSellingPrice) }}</span></div>
        </div>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'

const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ 'update:open': [value: boolean] }>()

const openModel = computed({
  get: () => props.open,
  set: (value: boolean) => emit('update:open', value)
})

const APPAREL_THRESHOLD = 2499
const APPAREL_LOWER_RATE = 5
const APPAREL_HIGHER_RATE = 18

const mrp = ref(0)
const productType = ref<'apparel' | 'fabric'>('apparel')
const fabricTaxRate = ref(5)
const discountPercent = ref(0)
const discountAmount = ref(0)
const discountMode = ref<'percent' | 'amount'>('percent')

const typeItems = [
  { label: 'Apparel', value: 'apparel' },
  { label: 'Fabric', value: 'fabric' }
]

function round2(value: number) {
  return Math.round((value + Number.EPSILON) * 100) / 100
}

function money(value: number) {
  return formatIndianMoney(Number.isFinite(value) ? value : 0)
}

// Apparel rate is derived from MRP by trying the lower band first: if the resulting basic price
// still fits under the threshold, that rate is self-consistent; otherwise fall through to the
// higher band. The threshold applies to basic (taxable) value, not MRP - matching the exact
// constants/approach backend/Garmetix.Api/Billing/SaleReviewEndpoints.cs already uses.
const appliedRate = computed(() => {
  if (productType.value === 'fabric') {
    return fabricTaxRate.value || 0
  }
  if (!mrp.value || mrp.value <= 0) {
    return APPAREL_LOWER_RATE
  }
  const basicAtLowerRate = mrp.value / (1 + APPAREL_LOWER_RATE / 100)
  return basicAtLowerRate <= APPAREL_THRESHOLD ? APPAREL_LOWER_RATE : APPAREL_HIGHER_RATE
})

const basicBeforeDiscount = computed(() => {
  if (!mrp.value || mrp.value <= 0) return 0
  return round2(mrp.value / (1 + appliedRate.value / 100))
})
const taxBeforeDiscount = computed(() => round2(mrp.value - basicBeforeDiscount.value))

const effectiveDiscountAmount = computed(() => {
  const basic = basicBeforeDiscount.value
  if (discountMode.value === 'amount') {
    return Math.min(Math.max(discountAmount.value || 0, 0), basic)
  }
  return round2((basic * Math.max(discountPercent.value || 0, 0)) / 100)
})

watch([discountPercent, basicBeforeDiscount], () => {
  if (discountMode.value !== 'percent') return
  discountAmount.value = round2((basicBeforeDiscount.value * Math.max(discountPercent.value || 0, 0)) / 100)
})
watch([discountAmount, basicBeforeDiscount], () => {
  if (discountMode.value !== 'amount') return
  const basic = basicBeforeDiscount.value
  discountPercent.value = basic > 0 ? round2(((discountAmount.value || 0) / basic) * 100) : 0
})

const adjustedBasic = computed(() => Math.max(0, round2(basicBeforeDiscount.value - effectiveDiscountAmount.value)))
const adjustedTax = computed(() => round2(adjustedBasic.value * (appliedRate.value / 100)))
const netSellingPrice = computed(() => round2(adjustedBasic.value + adjustedTax.value))
</script>
