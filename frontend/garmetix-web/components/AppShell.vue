<script setup lang="ts">
import {
  BadgeIndianRupee,
  Banknote,
  Boxes,
  Building2,
  CircleDollarSign,
  FileDown,
  FileText,
  PackagePlus,
  ReceiptIndianRupee,
  ShieldCheck,
  Shirt,
  Store,
  UserRoundCog,
  UsersRound
} from 'lucide-vue-next'

defineProps<{
  title: string
  companies?: any[]
  stores?: any[]
}>()

defineEmits<{
  refresh: []
}>()

const auth = useAuth()
const route = useRoute()

const modules = [
  { to: '/', label: 'Dashboard', icon: Building2 },
  { to: '/setup', label: 'Setup', icon: Building2 },
  { to: '/billing', label: 'Billing', icon: ReceiptIndianRupee },
  { to: '/inventory', label: 'Inventory', icon: Boxes },
  { to: '/purchase', label: 'Purchase', icon: PackagePlus },
  { to: '/vouchers', label: 'Vouchers', icon: Banknote },
  { to: '/petty-cash', label: 'Petty Cash', icon: CircleDollarSign },
  { to: '/hr', label: 'HR', icon: UsersRound },
  { to: '/payroll', label: 'Payroll', icon: BadgeIndianRupee },
  { to: '/reports', label: 'Reports', icon: FileText },
  { to: '/access', label: 'Access', icon: ShieldCheck },
  { to: '/import-export', label: 'Import Export', icon: FileDown }
]

function isActive(to: string) {
  return to === '/' ? route.path === '/' : route.path.startsWith(to)
}

function logout() {
  auth.logout()
  navigateTo('/')
}
</script>

<template>
  <div class="app-shell">
    <aside class="sidebar">
      <NuxtLink class="brand brand-link" to="/">
        <div class="brand-mark">
          <Shirt :size="21" />
        </div>
        <div>
          <p class="brand-title">Garmetix</p>
          <p class="brand-subtitle">Store management</p>
        </div>
      </NuxtLink>

      <nav class="nav">
        <NuxtLink
          v-for="item in modules"
          :key="item.to"
          class="nav-button"
          :class="{ active: isActive(item.to) }"
          :to="item.to"
        >
          <component :is="item.icon" :size="18" />
          <span>{{ item.label }}</span>
        </NuxtLink>
      </nav>
    </aside>

    <main class="main">
      <header class="topbar">
        <div>
          <h1>{{ title }}</h1>
        </div>
        <div class="context-controls">
          <select class="select" aria-label="Company">
            <option>All Companies</option>
            <option v-for="company in companies || []" :key="company.id">{{ company.name || company.companyName }}</option>
          </select>
          <select class="select" aria-label="Store">
            <option>All Stores</option>
            <option v-for="storeItem in stores || []" :key="storeItem.id">{{ storeItem.name || storeItem.storeName }}</option>
          </select>
          <button class="button secondary" type="button" @click="$emit('refresh')">
            <Store :size="16" />
            Sync
          </button>
          <button class="button secondary" type="button" @click="logout">
            <UserRoundCog :size="16" />
            Logout
          </button>
        </div>
      </header>

      <slot />
    </main>
  </div>
</template>
