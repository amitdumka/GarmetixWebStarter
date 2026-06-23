import type { FrontendAppId } from '@garmetix/shared-types'

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

export interface ShellRouteDefinition {
  id: string
  path: string
  label: string
  icon: string
  targetApp: FrontendAppId
  moduleKey: string
  moduleLabel: string
  roles: string[]
  externalUrlEnvKey?: string
  externalUrlEnvAliases?: string[]
  legacyPath: string
  showInMenu: boolean
  status: string
}

export interface ShellAppLink {
  id: FrontendAppId
  label: string
  href?: string
  configured: boolean
  current: boolean
}

export interface ShellRouteLink {
  id: string
  label: string
  href: string
  icon: string
  status: string
  roles: string[]
  external: boolean
}

export interface ShellRouteGroup {
  key: string
  label: string
  routes: ShellRouteLink[]
}

export interface ShellModel {
  appId: FrontendAppId
  title: string
  subtitle: string
  badge: string
  routeCount: number
  groups: ShellRouteGroup[]
  appLinks: ShellAppLink[]
}

const appCopy: Record<FrontendAppId, { title: string, subtitle: string, badge: string }> = {
  main: {
    title: 'Garmetix Back Office',
    subtitle: 'Dashboard, purchase, inventory, reports, customers and store operations.',
    badge: 'Back Office'
  },
  pos: {
    title: 'Garmetix POS',
    subtitle: 'Fast counter shell for sale, returns, off-book flows and day closing.',
    badge: 'POS'
  },
  hr: {
    title: 'Garmetix HR',
    subtitle: 'Employees, attendance, monthly attendance, payroll and salary payments.',
    badge: 'HR'
  },
  'ai-sense': {
    title: 'Garmetix AI Sense',
    subtitle: 'Read-only analytics shell for sales, purchase, stock risk and business trends.',
    badge: 'AI Sense'
  },
  books: {
    title: 'Garmetix Books',
    subtitle: 'Accounting, vouchers, petty cash, GST, audit and CA workflows.',
    badge: 'Books'
  },
  admin: {
    title: 'Garmetix Admin SaaS',
    subtitle: 'Owner/developer controls for setup, access, license, logs and deployment readiness.',
    badge: 'Admin/SaaS'
  }
}

export function resolveShellRouteHref(route: ShellRouteDefinition, env: Record<string, string | undefined>) {
  if (route.targetApp === 'main') return route.path

  const keys = [route.externalUrlEnvKey, ...(route.externalUrlEnvAliases ?? [])].filter(Boolean) as string[]
  const baseUrl = keys.map(key => env[key]).find(Boolean)
  if (!baseUrl) return route.legacyPath

  return `${baseUrl.replace(/\/+$/, '')}/${route.path.replace(/^\/+/, '')}`
}

export function buildShellRouteGroups(appId: FrontendAppId, routes: ShellRouteDefinition[], env: Record<string, string | undefined>) {
  const groups = new Map<string, ShellRouteGroup>()
  for (const route of routes) {
    if (route.targetApp !== appId || !route.showInMenu) continue

    const group = groups.get(route.moduleKey) ?? {
      key: route.moduleKey,
      label: route.moduleLabel,
      routes: []
    }

    const href = resolveShellRouteHref(route, env)
    group.routes.push({
      id: route.id,
      label: route.label,
      href,
      icon: route.icon,
      status: route.status,
      roles: route.roles,
      external: href.startsWith('http://') || href.startsWith('https://')
    })
    groups.set(route.moduleKey, group)
  }

  return [...groups.values()]
}

export function buildAppShellModel(options: {
  appId: FrontendAppId
  routes: ShellRouteDefinition[]
  env?: Record<string, string | undefined>
  appLinks?: ShellAppLink[]
}): ShellModel {
  const env = options.env ?? {}
  const groups = buildShellRouteGroups(options.appId, options.routes, env)
  const copy = appCopy[options.appId]

  return {
    appId: options.appId,
    title: copy.title,
    subtitle: copy.subtitle,
    badge: copy.badge,
    routeCount: groups.reduce((count, group) => count + group.routes.length, 0),
    groups,
    appLinks: options.appLinks ?? []
  }
}
