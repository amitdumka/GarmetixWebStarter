<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="text-sm text-muted">Salary payment</p>
          <h2 class="mt-1 text-2xl font-semibold">Salary Payments</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Payment records are read-only here. Generation will stay in the legacy app until this screen gets review and approval controls.
          </p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-3">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-xs text-muted">{{ card.label }}</p>
        <p class="mt-1 text-xl font-semibold">{{ card.value }}</p>
      </div>
    </div>

    <div class="overflow-hidden border border-default bg-muted/10">
      <div class="border-b border-default p-4">
        <h3 class="text-base font-semibold">Payments</h3>
      </div>
      <div class="overflow-auto">
        <table class="w-full min-w-[760px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Voucher</th>
              <th class="px-3 py-2">Employee</th>
              <th class="px-3 py-2">Month</th>
              <th class="px-3 py-2">Paid</th>
              <th class="px-3 py-2">Status</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(payment, index) in payments" :key="readText(payment, ['id'], String(index))" class="border-t border-default">
              <td class="px-3 py-2 font-medium">{{ readText(payment, ['voucherNumber', 'salaryPaymentNumber', 'number']) }}</td>
              <td class="px-3 py-2">{{ readText(payment, ['employeeName', 'employee']) }}</td>
              <td class="px-3 py-2">{{ readText(payment, ['monthYear', 'month']) }}</td>
              <td class="px-3 py-2 font-semibold">{{ formatIndianMoney(readNumber(payment, ['paidAmount', 'amount'])) }}</td>
              <td class="px-3 py-2">{{ readText(payment, ['status']) }}</td>
            </tr>
            <tr v-if="!payments.length">
              <td class="px-3 py-6 text-center text-muted" colspan="5">No salary payments returned.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readNumber, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Salary Payments - Garmetix HR' })

const { get } = useHrApiClient()
const loading = ref(false)
const error = ref('')
const payments = ref<ApiRecord[]>([])
const cards = computed(() => {
  const paid = payments.value.reduce((total, item) => total + readNumber(item, ['paidAmount', 'amount']), 0)
  return [
    { label: 'Payments', value: payments.value.length },
    { label: 'Paid Amount', value: formatIndianMoney(paid) },
    { label: 'Pending', value: payments.value.filter(item => readText(item, ['status']).toLowerCase().includes('pending')).length }
  ]
})

async function load() {
  loading.value = true
  error.value = ''
  try {
    const response = await get<ApiRecord[]>('api/salary-payments')
    payments.value = Array.isArray(response) ? response : []
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load salary payments.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>
