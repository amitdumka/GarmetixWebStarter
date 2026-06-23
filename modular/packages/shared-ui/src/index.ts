export interface AppShellLink {
  label: string
  to: string
  icon: string
}

export interface AppShellDefinition {
  title: string
  subtitle: string
  links: AppShellLink[]
}

export const baseShellLinks: AppShellLink[] = [
  { label: 'Dashboard', to: '/', icon: 'i-lucide-layout-dashboard' },
  { label: 'Status', to: '/status', icon: 'i-lucide-activity' },
  { label: 'Notifications', to: '/notifications', icon: 'i-lucide-bell' }
]

