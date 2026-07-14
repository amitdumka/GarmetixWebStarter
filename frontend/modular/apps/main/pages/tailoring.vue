<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-scissors" class="size-4" /> Sales</p>
          <h2 class="garmetix-dashboard-title">Tailoring &amp; Alteration</h2>
          <p class="garmetix-dashboard-subtitle">
            Separate stitching and alteration workflows with customer lookup, multi-line service orders, 5% GST service invoices, delivery dashboards and vendor costing.
          </p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="primary" :loading="loading" @click="loadAll">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="grid gap-3 md:grid-cols-5">
      <div class="garmetix-metric-card"><p class="garmetix-metric-label">Pending</p><p class="garmetix-metric-value">{{ readNumber(dashboard, ['pendingOrders']) }}</p></div>
      <div class="garmetix-metric-card"><p class="garmetix-metric-label">Due Today</p><p class="garmetix-metric-value">{{ readNumber(dashboard, ['dueToday']) }}</p></div>
      <div class="garmetix-metric-card"><p class="garmetix-metric-label">Late</p><p class="garmetix-metric-value text-error">{{ readNumber(dashboard, ['overdue']) }}</p></div>
      <div class="garmetix-metric-card"><p class="garmetix-metric-label">Ready</p><p class="garmetix-metric-value text-success">{{ readNumber(dashboard, ['readyForDelivery']) }}</p></div>
      <div class="garmetix-metric-card"><p class="garmetix-metric-label">Customer Due</p><p class="garmetix-metric-value text-base">{{ money(readNumber(dashboard, ['customerBalance'])) }}</p></div>
    </section>

    <div class="flex flex-wrap gap-2">
      <UButton size="sm" :variant="activeTab === 'stitching' ? 'solid' : 'soft'" color="neutral" icon="i-lucide-shirt" @click="activeTab = 'stitching'">Tailoring / Stitching</UButton>
      <UButton size="sm" :variant="activeTab === 'alteration' ? 'solid' : 'soft'" color="neutral" icon="i-lucide-scissors" @click="activeTab = 'alteration'">Alteration</UButton>
      <UButton size="sm" :variant="activeTab === 'deliveries' ? 'solid' : 'soft'" color="neutral" icon="i-lucide-calendar-days" @click="activeTab = 'deliveries'">Delivery Board</UButton>
      <UButton size="sm" :variant="activeTab === 'orders' ? 'solid' : 'soft'" color="neutral" icon="i-lucide-list-checks" @click="activeTab = 'orders'">All Orders</UButton>
      <UButton size="sm" :variant="activeTab === 'services' ? 'solid' : 'soft'" color="neutral" icon="i-lucide-badge-indian-rupee" @click="activeTab = 'services'">Service Master</UButton>
      <UButton size="sm" :variant="activeTab === 'vendors' ? 'solid' : 'soft'" color="neutral" icon="i-lucide-users-round" @click="activeTab = 'vendors'">Vendors &amp; Rates</UButton>
    </div>

    <!-- ============ STITCHING ============ -->
    <section v-if="activeTab === 'stitching'" class="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title">New Tailoring / Stitching Service Order</h3>
        <p class="garmetix-panel-subtitle mb-3">Customer mobile lookup, measurements, multiple items and expected delivery date.</p>
        <div class="grid gap-3 sm:grid-cols-2">
          <UFormField label="Store"><USelect v-model="stitching.storeId" :items="storeItems" @update:model-value="onOrderStoreChanged(stitching)" /></UFormField>
          <UFormField label="Expected delivery"><UInput v-model="stitching.expectedDeliveryDate" type="date" /></UFormField>
          <UFormField label="Customer mobile">
            <div class="flex gap-2">
              <UInput v-model="stitching.customerMobile" placeholder="Mobile number" />
              <UButton icon="i-lucide-search" color="neutral" variant="soft" @click="lookupCustomer(stitching)">Fetch</UButton>
            </div>
          </UFormField>
          <UFormField label="Customer"><USelectMenu v-model="stitching.customerId" :items="customerItems" value-key="value" searchable /></UFormField>
          <UFormField label="Tailoring vendor"><USelect v-model="stitching.vendorId" :items="vendorOptionItems" @update:model-value="applyVendorRates(stitching)" /></UFormField>
          <UFormField label="Order notes"><UInput v-model="stitching.internalRemarks" placeholder="Counter/user remarks" /></UFormField>
          <UFormField class="sm:col-span-2" label="Customer instructions"><UTextarea v-model="stitching.customerInstructions" :rows="2" placeholder="Fit, style, fabric, delivery commitment" /></UFormField>
        </div>

        <div class="mt-4 space-y-3">
          <div v-for="(line, index) in stitching.lines" :key="index" class="rounded-lg border border-default p-3">
            <div class="mb-2 flex items-center justify-between">
              <p class="text-sm font-semibold">Item {{ index + 1 }}</p>
              <UButton v-if="stitching.lines.length > 1" size="xs" color="error" variant="soft" @click="removeLine(stitching, index)">Remove</UButton>
            </div>
            <div class="grid gap-2 sm:grid-cols-3">
              <UFormField label="Service"><USelect v-model="line.serviceItemId" :items="stitchingServiceItems" @update:model-value="applyService(line, stitching)" /></UFormField>
              <UFormField label="Service name"><UInput v-model="line.serviceName" /></UFormField>
              <UFormField label="Garment / item"><UInput v-model="line.garmentName" placeholder="Shirt, Pant, Suit" /></UFormField>
              <UFormField label="Qty"><UInput v-model.number="line.quantity" type="number" /></UFormField>
              <UFormField label="Customer rate"><UInput v-model.number="line.customerRate" type="number" /></UFormField>
              <UFormField label="Discount"><UInput v-model.number="line.discountAmount" type="number" /></UFormField>
              <UFormField label="Vendor rate"><UInput v-model.number="line.vendorRate" type="number" /></UFormField>
              <UFormField label="Delivery"><UInput v-model="line.expectedDeliveryDate" type="date" /></UFormField>
              <UFormField label="Measurements"><UInput v-model="line.measurementsJson" placeholder="Chest 40, Waist 34..." /></UFormField>
              <UFormField class="sm:col-span-3" label="Line instructions"><UTextarea v-model="line.instructions" :rows="2" /></UFormField>
            </div>
          </div>
          <UButton color="neutral" variant="soft" icon="i-lucide-plus" @click="addLine(stitching)">Add Tailoring Item</UButton>
        </div>

        <div class="mt-4 grid gap-2 rounded-lg bg-muted/30 p-3 text-sm sm:grid-cols-5">
          <span>Gross <b>{{ money(stitchingTotals.gross) }}</b></span>
          <span>Discount <b>{{ money(stitchingTotals.discount) }}</b></span>
          <span>GST 5% <b>{{ money(stitchingTotals.tax) }}</b></span>
          <span>Bill <b>{{ money(stitchingTotals.charge) }}</b></span>
          <span>Vendor cost <b>{{ money(stitchingTotals.cost) }}</b></span>
        </div>
        <UButton class="mt-4" icon="i-lucide-save" color="primary" :loading="saving" @click="saveOrder(stitching, 'Tailoring service order saved.')">Save Tailoring Order</UButton>
      </div>

      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Today / Tomorrow Delivery Focus</h3>
        <div class="space-y-3">
          <div v-for="group in deliveryGroups.slice(0, 2)" :key="group.key" class="rounded-lg border border-default p-3">
            <div class="mb-2 flex items-center justify-between">
              <p class="font-semibold">{{ group.title }}</p>
              <UBadge :color="group.color" variant="subtle">{{ group.items.length }}</UBadge>
            </div>
            <div v-for="order in group.items" :key="readText(order, ['id'])" class="flex items-center justify-between border-b border-default py-2 last:border-0">
              <div>
                <p class="text-sm font-medium">{{ readText(order, ['orderNumber']) }} - {{ readText(order, ['customerName']) }}</p>
                <p class="text-xs text-muted">{{ readText(order, ['orderType']) }} - {{ readText(order, ['customerMobileNumber']) }}</p>
              </div>
              <UButton size="xs" color="neutral" variant="soft" @click="updateStatus(order, 4, 'Ready from delivery board')">Ready</UButton>
            </div>
            <p v-if="!group.items.length" class="text-sm text-muted">Nothing due.</p>
          </div>
        </div>
      </div>
    </section>

    <!-- ============ ALTERATION ============ -->
    <section v-if="activeTab === 'alteration'" class="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title">New Alteration Order</h3>
        <p class="garmetix-panel-subtitle mb-3">Alteration is separate from stitching and links to the original sale invoice/product item.</p>
        <div class="grid gap-3 sm:grid-cols-2">
          <UFormField label="Store"><USelect v-model="alteration.storeId" :items="storeItems" @update:model-value="onOrderStoreChanged(alteration)" /></UFormField>
          <UFormField label="Expected delivery"><UInput v-model="alteration.expectedDeliveryDate" type="date" /></UFormField>
          <UFormField label="Customer mobile">
            <div class="flex gap-2">
              <UInput v-model="alteration.customerMobile" />
              <UButton icon="i-lucide-search" color="neutral" variant="soft" @click="lookupCustomer(alteration)">Fetch</UButton>
            </div>
          </UFormField>
          <UFormField label="Customer"><USelectMenu v-model="alteration.customerId" :items="customerItems" value-key="value" searchable @update:model-value="loadSourceInvoices(alteration)" /></UFormField>
          <UFormField label="Sale invoice"><USelect v-model="alteration.sourceInvoiceId" :items="sourceInvoiceItems" /></UFormField>
          <UFormField label="Invoice item / product"><USelect v-model="alteration.sourceInvoiceItemId" :items="sourceItemItems" /></UFormField>
          <UFormField label="Tailoring / alteration vendor"><USelect v-model="alteration.vendorId" :items="vendorOptionItems" @update:model-value="applyVendorRates(alteration)" /></UFormField>
          <UFormField label="Source barcode"><UInput v-model="alteration.sourceBarcode" /></UFormField>
          <UFormField class="sm:col-span-2" label="Alteration details"><UTextarea v-model="alteration.customerInstructions" :rows="2" placeholder="Length, fitting, waist, sleeve, damage repair" /></UFormField>
        </div>

        <div class="mt-4 space-y-3">
          <div v-for="(line, index) in alteration.lines" :key="index" class="rounded-lg border border-default p-3">
            <div class="mb-2 flex items-center justify-between">
              <p class="text-sm font-semibold">Alteration item {{ index + 1 }}</p>
              <UButton v-if="alteration.lines.length > 1" size="xs" color="error" variant="soft" @click="removeLine(alteration, index)">Remove</UButton>
            </div>
            <div class="grid gap-2 sm:grid-cols-3">
              <UFormField label="Alteration service"><USelect v-model="line.serviceItemId" :items="alterationServiceItems" @update:model-value="applyService(line, alteration)" /></UFormField>
              <UFormField label="Service name"><UInput v-model="line.serviceName" /></UFormField>
              <UFormField label="Garment"><UInput v-model="line.garmentName" /></UFormField>
              <UFormField label="Qty"><UInput v-model.number="line.quantity" type="number" /></UFormField>
              <UFormField label="Customer charge"><UInput v-model.number="line.customerRate" type="number" /></UFormField>
              <UFormField label="Discount"><UInput v-model.number="line.discountAmount" type="number" /></UFormField>
              <UFormField label="Vendor / in-house cost"><UInput v-model.number="line.vendorRate" type="number" /></UFormField>
              <UFormField label="Cost responsibility"><USelect v-model="line.costResponsibility" :items="responsibilityItems" /></UFormField>
              <UFormField label="Delivery"><UInput v-model="line.expectedDeliveryDate" type="date" /></UFormField>
              <UFormField class="sm:col-span-3" label="Work instructions"><UTextarea v-model="line.instructions" :rows="2" /></UFormField>
            </div>
          </div>
          <UButton color="neutral" variant="soft" icon="i-lucide-plus" @click="addLine(alteration)">Add Alteration Item</UButton>
        </div>

        <UAlert class="mt-4" color="warning" variant="subtle" title="In-house alteration cost impact" description="When cost responsibility is In-house expense, delivery applies alteration cost impact to the source stock item once and reduces actual margin." />
        <div class="mt-4 grid gap-2 rounded-lg bg-muted/30 p-3 text-sm sm:grid-cols-5">
          <span>Customer bill <b>{{ money(alterationTotals.charge) }}</b></span>
          <span>Discount <b>{{ money(alterationTotals.discount) }}</b></span>
          <span>GST 5% <b>{{ money(alterationTotals.tax) }}</b></span>
          <span>Vendor/in-house cost <b>{{ money(alterationTotals.cost) }}</b></span>
          <span>Margin impact <b>{{ money(alterationTotals.profit) }}</b></span>
        </div>
        <UButton class="mt-4" icon="i-lucide-save" color="primary" :loading="saving" @click="saveOrder(alteration, 'Alteration order saved.')">Save Alteration Order</UButton>
      </div>

      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Alteration Rules</h3>
        <div class="space-y-2 text-sm text-muted">
          <p>1. Customer is fetched by mobile number before creating an alteration.</p>
          <p>2. Original sale invoice and item are linked for audit/profit analysis.</p>
          <p>3. Customer can be charged more than vendor cost; margin is shown separately.</p>
          <p>4. In-house expense is applied to source item cost at delivery so margin is reduced.</p>
          <p>5. Service invoice uses 5% GST and can be printed in two copies.</p>
        </div>
      </div>
    </section>

    <!-- ============ DELIVERIES ============ -->
    <section v-if="activeTab === 'deliveries'" class="grid gap-4 lg:grid-cols-2">
      <div v-for="group in deliveryGroups" :key="group.key" class="garmetix-section-card">
        <div class="mb-3 flex items-center justify-between">
          <h3 class="garmetix-panel-title">{{ group.title }}</h3>
          <UBadge :color="group.color" variant="subtle">{{ group.items.length }}</UBadge>
        </div>
        <div v-if="!group.items.length" class="rounded-md border border-dashed border-default p-4 text-center text-sm text-muted">Nothing here.</div>
        <div v-for="order in group.items" :key="readText(order, ['id'])" class="mb-2 rounded-lg border border-default p-3">
          <div class="flex items-start justify-between gap-3">
            <div>
              <p class="text-sm font-semibold">{{ readText(order, ['orderNumber']) }} - {{ readText(order, ['customerName']) }}</p>
              <p class="text-xs text-muted">{{ formatDate(order.expectedDeliveryDate) }} - {{ readText(order, ['customerMobileNumber']) }} - {{ readText(order, ['vendorName'], 'In-house') }}</p>
            </div>
            <UBadge :color="badgeColor(order.status)" variant="subtle">{{ readText(order, ['status']) }}</UBadge>
          </div>
          <div class="mt-3 flex flex-wrap gap-2">
            <UButton size="xs" color="neutral" variant="soft" @click="updateStatus(order, 3, 'Processing from delivery board')">Processing</UButton>
            <UButton size="xs" color="neutral" variant="soft" @click="updateStatus(order, 4, 'Ready from delivery board')">Ready</UButton>
            <UButton size="xs" color="success" variant="soft" @click="updateStatus(order, 5, 'Delivered from delivery board')">Delivered</UButton>
            <UButton size="xs" color="neutral" variant="soft" @click="printDocument(order, false)">Print Order</UButton>
            <UButton size="xs" color="neutral" variant="soft" @click="convertToInvoice(order)">Convert to Invoice</UButton>
          </div>
        </div>
      </div>
    </section>

    <!-- ============ ORDERS ============ -->
    <section v-if="activeTab === 'orders'" class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
        <h3 class="garmetix-panel-title">Order History and Status</h3>
        <div class="flex flex-col gap-2 sm:flex-row">
          <USelect v-model="orderStatusFilter" :items="orderStatusFilterItems" class="sm:w-40" />
          <USelect v-model="orderTypeFilter" :items="orderTypeFilterItems" class="sm:w-36" />
          <UInput v-model="orderSearch" icon="i-lucide-search" placeholder="Search order, customer" class="sm:w-64" />
        </div>
      </div>

      <div v-if="!pagedOrders.length" class="rounded-md border border-dashed border-default p-6 text-center text-sm text-muted">No orders match the current filters.</div>
      <div v-for="order in pagedOrders" :key="readText(order, ['id'])" class="mb-3 rounded-lg border border-default p-3">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <div>
            <p class="text-sm font-semibold">{{ readText(order, ['orderNumber']) }} - {{ readText(order, ['customerName']) }}</p>
            <p class="text-xs text-muted">{{ readText(order, ['orderType']) }} - Delivery {{ formatDate(order.expectedDeliveryDate) }} - Vendor {{ readText(order, ['vendorName'], 'In-house / not assigned') }}</p>
          </div>
          <UBadge :color="badgeColor(order.status)" variant="subtle">{{ readText(order, ['status']) }}</UBadge>
        </div>
        <div class="mt-2 grid gap-2 text-sm sm:grid-cols-5">
          <span>Bill {{ money(readNumber(order, ['customerChargeAmount'])) }}</span>
          <span>Received {{ money(readNumber(order, ['customerReceivedAmount'])) }}</span>
          <span>Due {{ money(readNumber(order, ['customerBalanceAmount'])) }}</span>
          <span>Vendor due {{ money(readNumber(order, ['vendorBalanceAmount'])) }}</span>
          <span>Profit {{ money(readNumber(order, ['profitImpactAmount'])) }}</span>
        </div>
        <div class="mt-3 flex flex-wrap gap-2">
          <UButton size="xs" color="neutral" variant="soft" @click="openOrder(order)">History</UButton>
          <UButton size="xs" color="neutral" variant="soft" @click="updateStatus(order, 3)">Processing</UButton>
          <UButton size="xs" color="neutral" variant="soft" @click="updateStatus(order, 4)">Ready</UButton>
          <UButton size="xs" color="neutral" variant="soft" @click="updateStatus(order, 5)">Delivered</UButton>
          <UButton size="xs" color="neutral" variant="soft" @click="printDocument(order, false)">Print Order</UButton>
          <UButton size="xs" color="primary" variant="soft" @click="convertToInvoice(order)">Convert to Invoice</UButton>
          <UButton v-if="readText(order, ['serviceInvoiceNumber'], '')" size="xs" color="neutral" variant="soft" @click="printDocument(order, true)">Print Invoice</UButton>
          <UButton size="xs" color="neutral" variant="soft" @click="openPayment(order)">Receive Payment</UButton>
          <UButton v-if="readText(order, ['vendorName'], '')" size="xs" color="neutral" variant="soft" @click="openVendorPayment(order)">Pay Vendor</UButton>
          <UButton size="xs" color="error" variant="ghost" @click="cancelOrder(order)">Cancel</UButton>
        </div>
      </div>

      <div v-if="filteredOrders.length" class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
        <p>Page {{ orderPage }} of {{ orderTotalPages }} - {{ filteredOrders.length }} order(s)</p>
        <div class="flex items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="orderPage <= 1" @click="orderPage = Math.max(1, orderPage - 1)">Prev</UButton>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="orderPage >= orderTotalPages" @click="orderPage = Math.min(orderTotalPages, orderPage + 1)">Next</UButton>
        </div>
      </div>
    </section>

    <!-- ============ SERVICES ============ -->
    <section v-if="activeTab === 'services'" class="garmetix-section-card">
      <div class="mb-3 flex items-center justify-between">
        <h3 class="garmetix-panel-title">Service Master</h3>
        <UButton icon="i-lucide-plus" color="primary" variant="solid" @click="startCreateService">New Service</UButton>
      </div>
      <div class="overflow-hidden rounded-lg border border-default">
        <table class="w-full text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr><th class="px-3 py-2">Service</th><th class="px-3 py-2">Category</th><th class="px-3 py-2 text-right">GST</th><th class="px-3 py-2 text-right">Vendor rate</th><th class="px-3 py-2 text-right">Customer rate</th></tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-if="!serviceItems.length"><td colspan="5" class="px-3 py-8 text-center text-muted">No service items yet.</td></tr>
            <tr v-for="item in serviceItems" :key="readText(item, ['id'])">
              <td class="px-3 py-2">
                <p class="font-medium">{{ readText(item, ['name']) }}</p>
                <p class="text-xs text-muted">{{ readText(item, ['serviceCode']) }}</p>
              </td>
              <td class="px-3 py-2">{{ categoryText(item.category) }}</td>
              <td class="px-3 py-2 text-right">{{ readNumber(item, ['taxRate']) || 5 }}%</td>
              <td class="px-3 py-2 text-right">{{ money(readNumber(item, ['defaultVendorRate'])) }}</td>
              <td class="px-3 py-2 text-right font-medium">{{ money(readNumber(item, ['defaultCustomerRate'])) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- ============ VENDORS & RATES ============ -->
    <section v-if="activeTab === 'vendors'" class="grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
      <div class="space-y-4">
        <div class="garmetix-section-card">
          <div class="mb-2 flex items-center justify-between">
            <div>
              <h3 class="garmetix-panel-title">Tailoring / Alteration Vendors</h3>
              <p class="garmetix-panel-subtitle">Stitching job workers, alteration vendors, embroidery/finishing partners.</p>
            </div>
            <UButton icon="i-lucide-plus" color="primary" variant="solid" @click="startCreateVendor">New Vendor</UButton>
          </div>
          <div class="space-y-2">
            <div v-if="!vendors.length" class="rounded-md border border-dashed border-default p-4 text-center text-sm text-muted">No tailoring vendors yet.</div>
            <div v-for="vendor in vendors" :key="readText(vendor, ['id'])" class="flex items-center justify-between rounded-md border border-default p-2 text-sm">
              <span>{{ readText(vendor, ['name']) }} - {{ readText(vendor, ['mobileNumber'], 'No mobile') }}</span>
              <UBadge :color="vendor.active ? 'success' : 'neutral'" variant="subtle">{{ vendor.active ? 'Active' : 'Inactive' }}</UBadge>
            </div>
          </div>
        </div>

        <div class="garmetix-section-card">
          <div class="mb-2 flex items-center justify-between">
            <h3 class="garmetix-panel-title">Vendor-Specific Service Rate</h3>
            <UButton icon="i-lucide-plus" color="primary" variant="solid" @click="startCreateVendorRate">New Rate</UButton>
          </div>
          <p class="garmetix-panel-subtitle">Override customer/vendor rates for a specific vendor + service combination.</p>
        </div>
      </div>

      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Vendor Rate Matrix</h3>
        <div v-if="!vendorRates.length" class="rounded-md border border-dashed border-default p-6 text-center text-sm text-muted">No vendor-specific rates saved.</div>
        <div v-for="rate in vendorRates" :key="readText(rate, ['id'])" class="mb-2 rounded-lg border border-default p-3">
          <div class="flex items-start justify-between gap-3">
            <div>
              <p class="font-semibold">{{ readText(rate, ['vendorName']) }} - {{ readText(rate, ['serviceName']) }}</p>
              <p class="text-xs text-muted">{{ categoryText(rate.category) }} - Effective {{ formatDate(rate.effectiveFrom) }} - {{ rate.active ? 'Active' : 'Inactive' }}</p>
            </div>
            <UBadge color="primary" variant="subtle">Vendor {{ money(readNumber(rate, ['vendorRate'])) }}</UBadge>
          </div>
          <div class="mt-2 grid gap-2 text-sm sm:grid-cols-3">
            <span>Customer charge <b>{{ money(readNumber(rate, ['customerRate'])) }}</b></span>
            <span>Vendor rate <b>{{ money(readNumber(rate, ['vendorRate'])) }}</b></span>
            <span>Margin <b>{{ money(readNumber(rate, ['customerRate']) - readNumber(rate, ['vendorRate'])) }}</b></span>
          </div>
          <p v-if="readText(rate, ['remarks'], '')" class="mt-2 text-xs text-muted">{{ readText(rate, ['remarks']) }}</p>
        </div>
      </div>
    </section>

    <!-- ============ Order history slideover ============ -->
    <USlideover v-model:open="historyOpen" title="Order History" :description="readText(selectedOrder, ['orderNumber'])">
      <template #body>
        <div class="space-y-3">
          <div v-for="entry in orderHistoryRows" :key="readText(entry, ['id'])" class="rounded-md border border-default p-3 text-sm">
            <p class="font-medium">{{ readText(entry, ['action']) }}</p>
            <p class="text-muted">{{ formatDate(entry.eventDate) }} - {{ readText(entry, ['actor'], 'System') }}</p>
            <p v-if="readText(entry, ['remarks'], '')">{{ readText(entry, ['remarks']) }}</p>
          </div>
          <p v-if="!orderHistoryRows.length" class="text-sm text-muted">No history recorded yet.</p>
        </div>
      </template>
    </USlideover>

    <!-- ============ New Service modal ============ -->
    <UModal v-model:open="serviceModalOpen" title="New Service Item" :ui="{ content: 'sm:max-w-xl' }">
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="saveService">
          <UFormField label="Store" class="sm:col-span-2"><USelect v-model="serviceForm.storeId" :items="storeItems" /></UFormField>
          <UFormField label="Code"><UInput v-model="serviceForm.serviceCode" placeholder="STITCH-SHIRT" /></UFormField>
          <UFormField label="Name"><UInput v-model="serviceForm.name" placeholder="Shirt stitching / Pant alteration" /></UFormField>
          <UFormField label="Category"><USelect v-model="serviceForm.category" :items="serviceCategoryItems" /></UFormField>
          <UFormField label="GST rate"><UInput v-model.number="serviceForm.taxRate" type="number" /></UFormField>
          <UFormField label="Customer rate"><UInput v-model.number="serviceForm.defaultCustomerRate" type="number" /></UFormField>
          <UFormField label="Vendor rate"><UInput v-model.number="serviceForm.defaultVendorRate" type="number" /></UFormField>
          <UFormField label="HSN/SAC" class="sm:col-span-2"><UInput v-model="serviceForm.hsnCode" /></UFormField>
          <div class="flex justify-end gap-2 sm:col-span-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">Save Service</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <!-- ============ New Vendor modal ============ -->
    <UModal v-model:open="vendorModalOpen" title="New Tailoring / Alteration Vendor" :ui="{ content: 'sm:max-w-xl' }">
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="saveTailoringVendor">
          <UFormField label="Vendor name"><UInput v-model="vendorForm.name" placeholder="Vendor / Karigar name" /></UFormField>
          <UFormField label="Mobile"><UInput v-model="vendorForm.mobileNumber" /></UFormField>
          <UFormField label="City"><UInput v-model="vendorForm.city" /></UFormField>
          <UFormField label="Email"><UInput v-model="vendorForm.email" /></UFormField>
          <UFormField label="Address" class="sm:col-span-2"><UTextarea v-model="vendorForm.address" :rows="2" placeholder="Workshop / home address" /></UFormField>
          <UFormField label="GSTIN"><UInput v-model="vendorForm.gstin" /></UFormField>
          <label class="flex items-center gap-2 text-sm"><UCheckbox v-model="vendorForm.active" /> Active vendor</label>
          <div class="flex justify-end gap-2 sm:col-span-2">
            <UButton type="submit" icon="i-lucide-user-plus" color="primary" :loading="saving">Add Vendor</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <!-- ============ New Vendor Rate modal ============ -->
    <UModal v-model:open="vendorRateModalOpen" title="New Vendor Service Rate" :ui="{ content: 'sm:max-w-xl' }">
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="saveVendorRate">
          <UFormField label="Store"><USelect v-model="vendorRateForm.storeId" :items="storeItems" /></UFormField>
          <UFormField label="Vendor"><USelect v-model="vendorRateForm.vendorId" :items="tailoringVendorItems" /></UFormField>
          <UFormField label="Service item" class="sm:col-span-2"><USelect v-model="vendorRateForm.serviceItemId" :items="allServiceItems" @update:model-value="seedVendorRateFromService" /></UFormField>
          <UFormField label="Effective from"><UInput v-model="vendorRateForm.effectiveFrom" type="date" /></UFormField>
          <UFormField label="Customer charge"><UInput v-model.number="vendorRateForm.customerRate" type="number" /></UFormField>
          <UFormField label="Vendor work rate" class="sm:col-span-2"><UInput v-model.number="vendorRateForm.vendorRate" type="number" /></UFormField>
          <UFormField label="Remarks" class="sm:col-span-2"><UTextarea v-model="vendorRateForm.remarks" :rows="2" /></UFormField>
          <label class="flex items-center gap-2 text-sm sm:col-span-2"><UCheckbox v-model="vendorRateForm.active" /> Active rate</label>
          <div class="flex justify-end gap-2 sm:col-span-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">Save Vendor Rate</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <!-- ============ Receive Payment modal ============ -->
    <UModal v-model:open="paymentModalOpen" title="Receive Customer Payment" :ui="{ content: 'sm:max-w-md' }">
      <template #body>
        <form class="grid gap-3" @submit.prevent="receivePayment">
          <p class="text-sm text-muted">{{ readText(paymentTarget, ['orderNumber']) }} - Due {{ money(readNumber(paymentTarget, ['customerBalanceAmount'])) }}</p>
          <UFormField label="Date"><UInput v-model="paymentForm.onDate" type="date" /></UFormField>
          <UFormField label="Amount"><UInput v-model.number="paymentForm.amount" type="number" /></UFormField>
          <UFormField label="Payment mode"><USelect v-model="paymentForm.paymentMode" :items="paymentModeItems" /></UFormField>
          <UFormField v-if="paymentForm.paymentMode !== 0" label="Bank account"><USelect v-model="paymentForm.bankAccountId" :items="bankAccountItems" /></UFormField>
          <UFormField label="Reference"><UInput v-model="paymentForm.referenceNumber" /></UFormField>
          <UFormField label="Remarks"><UTextarea v-model="paymentForm.remarks" :rows="2" /></UFormField>
          <div class="flex justify-end gap-2">
            <UButton type="submit" icon="i-lucide-hand-coins" color="primary" :loading="saving">Record Payment</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <!-- ============ Pay Vendor modal ============ -->
    <UModal v-model:open="vendorPaymentModalOpen" title="Pay Tailoring Vendor" :ui="{ content: 'sm:max-w-md' }">
      <template #body>
        <form class="grid gap-3" @submit.prevent="payVendor">
          <p class="text-sm text-muted">{{ readText(paymentTarget, ['orderNumber']) }} - Vendor due {{ money(readNumber(paymentTarget, ['vendorBalanceAmount'])) }}</p>
          <UFormField label="Date"><UInput v-model="vendorPaymentForm.onDate" type="date" /></UFormField>
          <UFormField label="Amount"><UInput v-model.number="vendorPaymentForm.amount" type="number" /></UFormField>
          <UFormField label="Payment mode"><USelect v-model="vendorPaymentForm.paymentMode" :items="paymentModeItems" /></UFormField>
          <UFormField v-if="vendorPaymentForm.paymentMode !== 0" label="Bank account"><USelect v-model="vendorPaymentForm.bankAccountId" :items="bankAccountItems" /></UFormField>
          <UFormField label="Reference"><UInput v-model="vendorPaymentForm.referenceNumber" /></UFormField>
          <UFormField label="Remarks"><UTextarea v-model="vendorPaymentForm.remarks" :rows="2" /></UFormField>
          <div class="flex justify-end gap-2">
            <UButton type="submit" icon="i-lucide-hand-coins" color="primary" :loading="saving">Pay Vendor</UButton>
          </div>
        </form>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { getStoredUser } from '@garmetix/shared-auth'
import { formatIndianMoney } from '@garmetix/shared-utils'
import { formatDate, readArray, readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../utils/main-api'

useHead({ title: 'Tailoring & Alteration - Garmetix Back Office' })

const { get, post } = useMainApiClient()

const serviceCategoryItems = [
  { label: 'Stitching', value: 0 }, { label: 'Alteration', value: 1 }, { label: 'Measurement', value: 2 },
  { label: 'Finishing', value: 3 }, { label: 'Other', value: 4 }
]
const responsibilityItems = [
  { label: 'Customer chargeable', value: 0 }, { label: 'In-house expense / store absorbs', value: 1 }, { label: 'Complimentary', value: 2 }
]
const paymentModeItems = [
  { label: 'Cash', value: 0 }, { label: 'Card', value: 1 }, { label: 'UPI', value: 2 }, { label: 'Wallet', value: 3 },
  { label: 'IMPS', value: 4 }, { label: 'RTGS', value: 5 }, { label: 'NEFT', value: 6 }, { label: 'Cheque', value: 7 }, { label: 'Other', value: 14 }
]
const statusOptions = [
  { label: 'Ordered', value: 1 }, { label: 'Sent to vendor', value: 2 }, { label: 'Processing', value: 3 },
  { label: 'Ready', value: 4 }, { label: 'Delivered', value: 5 }, { label: 'Completed', value: 9 }
]
const orderStatusFilterItems = [
  { label: 'All status', value: 'all' },
  { label: 'Ordered', value: 'Ordered' }, { label: 'Sent to vendor', value: 'SentToVendor' }, { label: 'In progress', value: 'InProgress' },
  { label: 'Ready', value: 'ReadyForDelivery' }, { label: 'Delivered', value: 'Delivered' }, { label: 'Invoiced', value: 'Invoiced' },
  { label: 'Completed', value: 'Completed' }, { label: 'Cancelled', value: 'Cancelled' }
]
const orderTypeFilterItems = [
  { label: 'All types', value: 'all' }, { label: 'Stitching', value: 'Stitching' }, { label: 'Alteration', value: 'Alteration' }
]

function localDateValue(date = new Date()) {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000)
  return local.toISOString().slice(0, 10)
}
function money(value: number) { return formatIndianMoney(value) }
function categoryText(value: unknown) { return serviceCategoryItems.find(item => item.value === Number(value))?.label || 'Service' }
function badgeColor(status: unknown) {
  const value = String(status ?? '')
  if (value === 'ReadyForDelivery') return 'success' as const
  if (value === 'Delivered' || value === 'Completed' || value === 'Invoiced' || value === 'Paid') return 'primary' as const
  if (value === 'Cancelled') return 'error' as const
  return 'warning' as const
}
function statusText(status: number) { return statusOptions.find(item => item.value === status)?.label || 'updated' }

function emptyOrder(orderType: number) {
  return reactive({
    companyId: '', storeGroupId: '', storeId: '', orderType,
    customerMobile: '', customerId: '' as string | null, vendorId: '__none' as string,
    sourceInvoiceId: '__none' as string, sourceInvoiceItemId: '__none' as string,
    sourceProductId: null as string | null, sourceProductName: '', sourceBarcode: '',
    expectedDeliveryDate: localDateValue(new Date(Date.now() + 3 * 86400000)),
    measurementsJson: '', customerInstructions: '', internalRemarks: '',
    lines: [emptyLine(orderType)]
  })
}
function emptyLine(orderType = 0) {
  return {
    serviceItemId: null as string | null, serviceName: '', category: orderType === 1 ? 1 : 0,
    garmentName: '', barcode: '', quantity: 1, customerRate: 0, vendorRate: 0, discountAmount: 0,
    costResponsibility: 0, expectedDeliveryDate: localDateValue(new Date(Date.now() + 3 * 86400000)),
    measurementsJson: '', instructions: '', vendorRemarks: ''
  }
}
function emptyVendorForm() { return { companyId: '', name: '', mobileNumber: '', address: '', city: '', email: '', gstin: '', active: true } }
function emptyVendorRateForm() { return { companyId: '', storeGroupId: '', storeId: '', vendorId: '', serviceItemId: '', customerRate: 0, vendorRate: 0, effectiveFrom: localDateValue(), active: true, remarks: '' } }
function emptyServiceForm() { return { companyId: '', storeGroupId: '', storeId: '', serviceCode: '', name: '', category: 0, defaultCustomerRate: 0, defaultVendorRate: 0, taxRate: 5, hsnCode: '9988', productId: null as string | null, active: true, remarks: '' } }

const loading = ref(false)
const saving = ref(false)
const error = ref('')
const message = ref('')
const activeTab = ref<'stitching' | 'alteration' | 'deliveries' | 'orders' | 'services' | 'vendors'>('stitching')

const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const customers = ref<ApiRecord[]>([])
const vendors = ref<ApiRecord[]>([])
const vendorRates = ref<ApiRecord[]>([])
const serviceItems = ref<ApiRecord[]>([])
const orders = ref<ApiRecord[]>([])
const dashboard = ref<ApiRecord | null>(null)
const deliveries = ref<ApiRecord | null>(null)
const sourceInvoices = ref<ApiRecord[]>([])
const sourceInvoiceItemsData = ref<ApiRecord[]>([])
const bankAccounts = ref<ApiRecord[]>([])
const selectedOrder = ref<ApiRecord | null>(null)
const historyOpen = ref(false)
const paymentTarget = ref<ApiRecord | null>(null)

const stitching = emptyOrder(0)
const alteration = emptyOrder(1)
const serviceForm = reactive(emptyServiceForm())
const vendorForm = reactive(emptyVendorForm())
const vendorRateForm = reactive(emptyVendorRateForm())
const paymentForm = reactive({ onDate: localDateValue(), amount: 0, paymentMode: 0, bankAccountId: '' as string | null, referenceNumber: '', remarks: '' })
const vendorPaymentForm = reactive({ onDate: localDateValue(), amount: 0, paymentMode: 0, bankAccountId: '' as string | null, referenceNumber: '', remarks: '' })

const serviceModalOpen = ref(false)
const vendorModalOpen = ref(false)
const vendorRateModalOpen = ref(false)
const paymentModalOpen = ref(false)
const vendorPaymentModalOpen = ref(false)

const orderStatusFilter = ref('all')
const orderTypeFilter = ref('all')
const orderSearch = ref('')
const orderPage = ref(1)
const orderPageSize = 20

const storeItems = computed(() => stores.value.map(item => ({ label: readText(item, ['name'], 'Store'), value: readText(item, ['id'], '') })))
const customerItems = computed(() => customers.value.map(item => ({ label: `${readText(item, ['name'])} - ${readText(item, ['mobileNumber'])}`, value: readText(item, ['id'], '') })))
const vendorOptionItems = computed(() => [{ label: 'In-house / not assigned', value: '__none' }, ...vendors.value.map(item => ({ label: `${readText(item, ['name'])} - ${readText(item, ['mobileNumber'], 'No mobile')}`, value: readText(item, ['id'], '') }))])
const tailoringVendorItems = computed(() => vendors.value.map(item => ({ label: `${readText(item, ['name'])} - ${readText(item, ['mobileNumber'], 'No mobile')}`, value: readText(item, ['id'], '') })))
const stitchingServiceItems = computed(() => serviceItems.value.filter(item => Number(item.category) !== 1).map(item => ({ label: `${readText(item, ['name'])} - ${money(readNumber(item, ['defaultCustomerRate']))}`, value: readText(item, ['id'], '') })))
const alterationServiceItems = computed(() => serviceItems.value.filter(item => [1, 3, 4].includes(Number(item.category))).map(item => ({ label: `${readText(item, ['name'])} - ${money(readNumber(item, ['defaultCustomerRate']))}`, value: readText(item, ['id'], '') })))
const allServiceItems = computed(() => serviceItems.value.map(item => ({ label: `${readText(item, ['name'])} - ${categoryText(item.category)} - ${money(readNumber(item, ['defaultCustomerRate']))}`, value: readText(item, ['id'], '') })))
const sourceInvoiceItems = computed(() => [{ label: 'Select original sale invoice', value: '__none' }, ...sourceInvoices.value.map(item => ({ label: `${readText(item, ['invoiceNumber'])} - ${formatDate(item.onDate)} - ${money(readNumber(item, ['billAmount']))}`, value: readText(item, ['id'], '') }))])
const sourceItemItems = computed(() => [{ label: 'Select invoice item', value: '__none' }, ...sourceInvoiceItemsData.value.map(item => ({ label: `${readText(item, ['productName'], readText(item, ['barcode']))} - ${readText(item, ['barcode'])}`, value: readText(item, ['id'], '') }))])
const bankAccountItems = computed(() => bankAccounts.value.map(item => ({ label: readText(item, ['accountName', 'name'], 'Bank account'), value: readText(item, ['id'], '') })))

const deliveryGroups = computed(() => [
  { key: 'today', title: 'Today deliveries', color: 'primary' as const, items: readArray(deliveries.value, ['today']) },
  { key: 'tomorrow', title: 'Tomorrow deliveries', color: 'info' as const, items: readArray(deliveries.value, ['tomorrow']) },
  { key: 'late', title: 'Late delivery', color: 'error' as const, items: readArray(deliveries.value, ['late']) },
  { key: 'ready', title: 'Ready for delivery', color: 'success' as const, items: readArray(deliveries.value, ['ready']) }
])

function calculateTotals(lines: ReturnType<typeof emptyLine>[]) {
  return lines.reduce((total, line) => {
    const qty = Number(line.quantity || 0)
    const grossBeforeDiscount = qty * Number(line.customerRate || 0)
    const discount = Number(line.discountAmount || 0)
    const charge = Math.max(0, grossBeforeDiscount - discount)
    const cost = Math.max(0, qty * Number(line.vendorRate || 0))
    total.gross += grossBeforeDiscount
    total.discount += discount
    total.charge += charge
    total.taxable += charge / 1.05
    total.tax += charge - charge / 1.05
    total.cost += cost
    total.inHouse += Number(line.costResponsibility) === 1 ? cost : 0
    total.profit += charge - cost
    return total
  }, { gross: 0, discount: 0, charge: 0, taxable: 0, tax: 0, cost: 0, inHouse: 0, profit: 0 })
}
const stitchingTotals = computed(() => calculateTotals(stitching.lines))
const alterationTotals = computed(() => calculateTotals(alteration.lines))

const filteredOrders = computed(() => {
  const term = orderSearch.value.trim().toLowerCase()
  return orders.value.filter(order => {
    const statusMatches = orderStatusFilter.value === 'all' || readText(order, ['status']) === orderStatusFilter.value
    const typeMatches = orderTypeFilter.value === 'all' || readText(order, ['orderType']) === orderTypeFilter.value
    const textMatches = !term || [readText(order, ['orderNumber']), readText(order, ['customerName']), readText(order, ['vendorName'])].join(' ').toLowerCase().includes(term)
    return statusMatches && typeMatches && textMatches
  })
})
const orderTotalPages = computed(() => Math.max(1, Math.ceil(filteredOrders.value.length / orderPageSize)))
const pagedOrders = computed(() => filteredOrders.value.slice((orderPage.value - 1) * orderPageSize, orderPage.value * orderPageSize))
const orderHistoryRows = computed(() => readArray(selectedOrder.value, ['history']))
watch([orderSearch, orderStatusFilter, orderTypeFilter], () => { orderPage.value = 1 })

function hydrateDefaults(form: ReturnType<typeof emptyOrder>) {
  const storedUser = getStoredUser(window.localStorage)
  if (!form.companyId) form.companyId = storedUser?.companyId || readText(stores.value[0], ['companyId'], '')
  if (!form.storeId) form.storeId = storedUser?.storeId || readText(stores.value[0], ['id'], '')
  if (!form.storeGroupId) form.storeGroupId = storedUser?.storeGroupId || readText(stores.value[0], ['storeGroupId'], '')
}
function onOrderStoreChanged(form: ReturnType<typeof emptyOrder>) {
  const store = stores.value.find(item => readText(item, ['id'], '') === form.storeId)
  form.companyId = readText(store, ['companyId'], form.companyId)
  form.storeGroupId = readText(store, ['storeGroupId'], form.storeGroupId)
}
function hydrateServiceDefaults() {
  serviceForm.companyId = serviceForm.companyId || stitching.companyId
  serviceForm.storeGroupId = serviceForm.storeGroupId || stitching.storeGroupId
  serviceForm.storeId = serviceForm.storeId || stitching.storeId
}
function hydrateVendorDefaults() {
  vendorForm.companyId = vendorForm.companyId || stitching.companyId
  vendorRateForm.companyId = vendorRateForm.companyId || stitching.companyId
  vendorRateForm.storeGroupId = vendorRateForm.storeGroupId || stitching.storeGroupId
  vendorRateForm.storeId = vendorRateForm.storeId || stitching.storeId
}

async function loadAll() {
  loading.value = true
  error.value = ''
  try {
    const [companyData, storeData, customerData, vendorData, vendorRateData, serviceData, orderData, dashboardData, deliveryData, bankData] = await Promise.allSettled([
      get<unknown>('companies'),
      get<unknown>('stores'),
      get<unknown>('customers'),
      get<unknown>('tailoring/vendors'),
      get<unknown>('tailoring/vendor-rates'),
      get<unknown>('tailoring/service-items'),
      get<unknown>('tailoring/orders'),
      get<ApiRecord>('tailoring/dashboard'),
      get<ApiRecord>('tailoring/deliveries/overview'),
      get<unknown>('bank-accounts')
    ])
    if (companyData.status === 'fulfilled') companies.value = toRows(companyData.value)
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    if (customerData.status === 'fulfilled') customers.value = toRows(customerData.value)
    if (vendorData.status === 'fulfilled') vendors.value = toRows(vendorData.value)
    if (vendorRateData.status === 'fulfilled') vendorRates.value = toRows(vendorRateData.value)
    if (serviceData.status === 'fulfilled') serviceItems.value = toRows(serviceData.value)
    if (orderData.status === 'fulfilled') orders.value = toRows(orderData.value)
    if (dashboardData.status === 'fulfilled') dashboard.value = dashboardData.value
    if (deliveryData.status === 'fulfilled') deliveries.value = deliveryData.value
    if (bankData.status === 'fulfilled') bankAccounts.value = toRows(bankData.value)
    hydrateDefaults(stitching)
    hydrateDefaults(alteration)
    hydrateServiceDefaults()
    hydrateVendorDefaults()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not load tailoring and alteration workspace.'
  } finally {
    loading.value = false
  }
}

function normalizeVendorId(form: ReturnType<typeof emptyOrder>) { return form.vendorId && form.vendorId !== '__none' ? form.vendorId : '' }
function vendorSpecificRate(serviceItemId: string | null, vendorId: string) {
  if (!serviceItemId || !vendorId) return null
  return vendorRates.value.find(rate => readText(rate, ['serviceItemId']) === serviceItemId && readText(rate, ['vendorId']) === vendorId && rate.active !== false) || null
}
function applyVendorRateToLine(line: ReturnType<typeof emptyLine>, form: ReturnType<typeof emptyOrder>) {
  const vendorId = normalizeVendorId(form)
  const rate = vendorSpecificRate(line.serviceItemId, vendorId)
  if (!rate) return
  if (readNumber(rate, ['customerRate']) > 0) line.customerRate = readNumber(rate, ['customerRate'])
  line.vendorRate = readNumber(rate, ['vendorRate'])
}
function applyVendorRates(form: ReturnType<typeof emptyOrder>) { form.lines.forEach(line => applyVendorRateToLine(line, form)) }
function addLine(form: ReturnType<typeof emptyOrder>) { form.lines.push(emptyLine(form.orderType)) }
function removeLine(form: ReturnType<typeof emptyOrder>, index: number) { if (form.lines.length > 1) form.lines.splice(index, 1) }
function applyService(line: ReturnType<typeof emptyLine>, form?: ReturnType<typeof emptyOrder>) {
  const item = serviceItems.value.find(service => readText(service, ['id']) === line.serviceItemId)
  if (!item) return
  line.serviceName = readText(item, ['name'])
  line.category = readNumber(item, ['category'])
  line.customerRate = readNumber(item, ['defaultCustomerRate'])
  line.vendorRate = readNumber(item, ['defaultVendorRate'])
  if (form) applyVendorRateToLine(line, form)
}

async function lookupCustomer(form: ReturnType<typeof emptyOrder>) {
  if (!form.customerMobile || form.customerMobile.trim().length < 4) { error.value = 'Enter mobile number first.'; return }
  error.value = ''
  try {
    const customer = await get<ApiRecord>('tailoring/customers/by-mobile', { mobile: form.customerMobile.trim() })
    form.customerId = readText(customer, ['id'], '')
    form.customerMobile = readText(customer, ['mobileNumber'], form.customerMobile)
    message.value = `Customer found: ${readText(customer, ['name'])}`
    if (form.orderType === 1) await loadSourceInvoices(form)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Customer not found. Add customer first from Customer master.'
  }
}
async function loadSourceInvoices(form: ReturnType<typeof emptyOrder>) {
  if (!form.customerId && !form.customerMobile) return
  try {
    const query = form.customerId ? { customerId: form.customerId } : { mobile: form.customerMobile }
    sourceInvoices.value = toRows(await get<unknown>('tailoring/alteration/source-invoices', query))
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load source invoices.'
  }
}
watch(() => alteration.sourceInvoiceId, async () => {
  sourceInvoiceItemsData.value = []
  if (!alteration.sourceInvoiceId || alteration.sourceInvoiceId === '__none') return
  sourceInvoiceItemsData.value = toRows(await get<unknown>(`tailoring/alteration/source-invoices/${alteration.sourceInvoiceId}/items`))
})
watch(() => alteration.sourceInvoiceItemId, () => {
  const item = sourceInvoiceItemsData.value.find(source => readText(source, ['id']) === alteration.sourceInvoiceItemId)
  if (!item) return
  alteration.sourceProductId = readText(item, ['productId'], '')
  alteration.sourceProductName = readText(item, ['productName'], '')
  alteration.sourceBarcode = readText(item, ['barcode'], '')
  if (alteration.lines[0]) {
    alteration.lines[0].garmentName = alteration.sourceProductName
    alteration.lines[0].barcode = alteration.sourceBarcode
  }
})

function buildOrderPayload(form: ReturnType<typeof emptyOrder>) {
  return {
    companyId: form.companyId,
    storeGroupId: form.storeGroupId,
    storeId: form.storeId,
    orderType: form.orderType,
    customerId: form.customerId,
    vendorId: form.vendorId === '__none' ? null : form.vendorId,
    sourceInvoiceId: form.sourceInvoiceId === '__none' ? null : form.sourceInvoiceId,
    sourceInvoiceItemId: form.sourceInvoiceItemId === '__none' ? null : form.sourceInvoiceItemId,
    sourceProductId: form.sourceProductId || null,
    sourceProductName: form.sourceProductName || null,
    sourceBarcode: form.sourceBarcode || null,
    expectedDeliveryDate: form.expectedDeliveryDate || null,
    measurementsJson: form.measurementsJson || null,
    customerInstructions: form.customerInstructions || null,
    internalRemarks: form.internalRemarks || null,
    lines: form.lines.map(line => ({
      serviceItemId: line.serviceItemId || null,
      serviceName: line.serviceName,
      category: line.category,
      garmentName: line.garmentName || null,
      barcode: line.barcode || null,
      quantity: Number(line.quantity || 0),
      customerRate: Number(line.customerRate || 0),
      vendorRate: Number(line.vendorRate || 0),
      discountAmount: Number(line.discountAmount || 0),
      costResponsibility: line.costResponsibility,
      expectedDeliveryDate: line.expectedDeliveryDate || null,
      measurementsJson: line.measurementsJson || null,
      instructions: line.instructions || null,
      vendorRemarks: line.vendorRemarks || null
    }))
  }
}

async function saveOrder(form: ReturnType<typeof emptyOrder>, successMessage: string) {
  if (!form.customerId) { error.value = 'Fetch or select a customer first.'; return }
  saving.value = true
  error.value = ''
  try {
    await post<unknown>('tailoring/orders', buildOrderPayload(form))
    const orderType = form.orderType
    Object.assign(form, emptyOrder(orderType))
    hydrateDefaults(form)
    await loadAll()
    activeTab.value = 'orders'
    message.value = successMessage
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not save order.'
  } finally {
    saving.value = false
  }
}

function startCreateService() {
  Object.assign(serviceForm, emptyServiceForm())
  hydrateServiceDefaults()
  error.value = ''
  serviceModalOpen.value = true
}
async function saveService() {
  saving.value = true
  error.value = ''
  try {
    await post<unknown>('tailoring/service-items', {
      ...serviceForm,
      defaultCustomerRate: Number(serviceForm.defaultCustomerRate || 0),
      defaultVendorRate: Number(serviceForm.defaultVendorRate || 0),
      taxRate: Number(serviceForm.taxRate || 5)
    })
    message.value = 'Service item saved.'
    serviceModalOpen.value = false
    await loadAll()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not save service item.'
  } finally {
    saving.value = false
  }
}

function startCreateVendor() {
  Object.assign(vendorForm, emptyVendorForm(), { companyId: stitching.companyId })
  error.value = ''
  vendorModalOpen.value = true
}
async function saveTailoringVendor() {
  if (!vendorForm.name.trim() || !vendorForm.mobileNumber.trim()) { error.value = 'Vendor name and mobile number are required.'; return }
  saving.value = true
  error.value = ''
  try {
    await post<unknown>('tailoring/vendors', { ...vendorForm })
    message.value = 'Tailoring / alteration vendor added.'
    vendorModalOpen.value = false
    await loadAll()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not save tailoring vendor.'
  } finally {
    saving.value = false
  }
}

function startCreateVendorRate() {
  Object.assign(vendorRateForm, emptyVendorRateForm())
  hydrateVendorDefaults()
  error.value = ''
  vendorRateModalOpen.value = true
}
function seedVendorRateFromService() {
  const item = serviceItems.value.find(service => readText(service, ['id']) === vendorRateForm.serviceItemId)
  if (!item) return
  vendorRateForm.customerRate = readNumber(item, ['defaultCustomerRate'])
  vendorRateForm.vendorRate = readNumber(item, ['defaultVendorRate'])
}
async function saveVendorRate() {
  hydrateVendorDefaults()
  if (!vendorRateForm.vendorId || !vendorRateForm.serviceItemId) { error.value = 'Select vendor and service item first.'; return }
  saving.value = true
  error.value = ''
  try {
    await post<unknown>('tailoring/vendor-rates', {
      ...vendorRateForm,
      customerRate: Number(vendorRateForm.customerRate || 0),
      vendorRate: Number(vendorRateForm.vendorRate || 0)
    })
    message.value = 'Vendor service rate saved.'
    vendorRateModalOpen.value = false
    await loadAll()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not save vendor rate.'
  } finally {
    saving.value = false
  }
}

async function openOrder(order: ApiRecord) {
  error.value = ''
  try {
    selectedOrder.value = await get<ApiRecord>(`tailoring/orders/${readText(order, ['id'])}`)
    historyOpen.value = true
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load order history.'
  }
}
async function updateStatus(order: ApiRecord, status: number, remarks = '') {
  error.value = ''
  try {
    await post<unknown>(`tailoring/orders/${readText(order, ['id'])}/status`, { status, eventDate: localDateValue(), remarks: remarks || `Marked ${statusText(status)}` })
    await loadAll()
    message.value = `Order marked ${statusText(status)}.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to update order status.'
  }
}
async function cancelOrder(order: ApiRecord) {
  if (!window.confirm(`Cancel order ${readText(order, ['orderNumber'])}?`)) return
  error.value = ''
  try {
    await post<unknown>(`tailoring/orders/${readText(order, ['id'])}/cancel`, { remarks: 'Cancelled from orders list' })
    await loadAll()
    message.value = 'Order cancelled.'
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to cancel order.'
  }
}
async function convertToInvoice(order: ApiRecord) {
  error.value = ''
  try {
    await post<unknown>(`tailoring/orders/${readText(order, ['id'])}/convert-to-service-invoice`, { invoiceDate: localDateValue(), additionalPaidAmount: 0, additionalPaymentMode: 0, bankAccountId: null, referenceNumber: '', remarks: 'Converted from tailoring order' })
    await loadAll()
    message.value = 'Service invoice created with 5% GST.'
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to convert order to service invoice.'
  }
}

function openPayment(order: ApiRecord) {
  paymentTarget.value = order
  Object.assign(paymentForm, { onDate: localDateValue(), amount: readNumber(order, ['customerBalanceAmount']), paymentMode: 0, bankAccountId: '', referenceNumber: '', remarks: '' })
  error.value = ''
  paymentModalOpen.value = true
}
async function receivePayment() {
  const id = readText(paymentTarget.value, ['id'], '')
  if (!id || !paymentForm.amount) return
  saving.value = true
  error.value = ''
  try {
    await post<unknown>(`tailoring/orders/${id}/receive-payment`, { ...paymentForm, amount: Number(paymentForm.amount || 0) })
    message.value = 'Customer receipt saved.'
    paymentModalOpen.value = false
    await loadAll()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to record customer payment.'
  } finally {
    saving.value = false
  }
}

function openVendorPayment(order: ApiRecord) {
  paymentTarget.value = order
  Object.assign(vendorPaymentForm, { onDate: localDateValue(), amount: readNumber(order, ['vendorBalanceAmount']), paymentMode: 0, bankAccountId: '', referenceNumber: '', remarks: '' })
  error.value = ''
  vendorPaymentModalOpen.value = true
}
async function payVendor() {
  const id = readText(paymentTarget.value, ['id'], '')
  if (!id || !vendorPaymentForm.amount) return
  saving.value = true
  error.value = ''
  try {
    await post<unknown>(`tailoring/orders/${id}/pay-vendor`, { ...vendorPaymentForm, amount: Number(vendorPaymentForm.amount || 0) })
    message.value = 'Vendor payment recorded.'
    vendorPaymentModalOpen.value = false
    await loadAll()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to record vendor payment.'
  } finally {
    saving.value = false
  }
}

function escapeHtml(value: unknown) {
  return String(value ?? '').replace(/[&<>"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' } as Record<string, string>)[char] || char)
}
function buildPrintHtml(data: ApiRecord) {
  const lineRows = readArray(data, ['lines']).map(line => `<tr><td>${escapeHtml(line.serviceName)}</td><td>${escapeHtml(line.garmentName || '')}</td><td>${readNumber(line, ['quantity'])}</td><td>${money(readNumber(line, ['customerRate']))}</td><td>${money(readNumber(line, ['discountAmount']))}</td><td>${money(readNumber(line, ['taxAmount']))}</td><td>${money(readNumber(line, ['lineTotal']))}</td></tr>`).join('')
  const copy = (label: string) => `<section class="copy"><h1>${escapeHtml(data.documentType)} <small>${label}</small></h1><div class="meta"><p><b>Order:</b> ${escapeHtml(data.orderNumber)}</p><p><b>Invoice:</b> ${escapeHtml(readText(data, ['serviceInvoiceNumber'], '-'))}</p><p><b>Customer:</b> ${escapeHtml(data.customerName)} / ${escapeHtml(readText(data, ['customerMobileNumber'], ''))}</p><p><b>Delivery:</b> ${escapeHtml(formatDate(data.expectedDeliveryDate))}</p></div><table><thead><tr><th>Service</th><th>Garment</th><th>Qty</th><th>Rate</th><th>Discount</th><th>GST 5%</th><th>Total</th></tr></thead><tbody>${lineRows}</tbody></table><div class="totals"><p>Taxable: ${money(readNumber(data, ['taxableAmount']))}</p><p>GST: ${money(readNumber(data, ['taxAmount']))}</p><p><b>Bill: ${money(readNumber(data, ['billAmount']))}</b></p><p>Paid: ${money(readNumber(data, ['paidAmount']))}</p><p>Balance: ${money(readNumber(data, ['balanceAmount']))}</p></div></section>`
  const copies = readArray(data, ['copies']).map(item => String(item))
  return `<!doctype html><html><head><title>${escapeHtml(data.documentType)}</title><style>body{font-family:Arial,sans-serif;color:#111}.copy{page-break-after:always;border:1px solid #ddd;padding:18px;margin:12px}h1{margin:0 0 12px}small{font-size:13px;color:#555}.meta{display:grid;grid-template-columns:repeat(2,1fr);gap:4px 18px;font-size:13px}table{width:100%;border-collapse:collapse;margin-top:12px}th,td{border:1px solid #ddd;padding:7px;font-size:12px;text-align:left}.totals{text-align:right;margin-top:12px}</style></head><body>${(copies.length ? copies : ['Customer Copy', 'Store Copy']).map(copy).join('')}</body></html>`
}
async function printDocument(order: ApiRecord, invoice = false) {
  error.value = ''
  try {
    const data = await get<ApiRecord>(`tailoring/orders/${readText(order, ['id'])}/${invoice ? 'print-invoice' : 'print-order'}`)
    const html = buildPrintHtml(data)
    const win = window.open('', '_blank', 'width=900,height=1000')
    if (!win) return
    win.document.write(html)
    win.document.close()
    win.focus()
    setTimeout(() => win.print(), 400)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load print document.'
  }
}

onMounted(loadAll)
</script>
