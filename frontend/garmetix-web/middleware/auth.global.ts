const publicPaths = new Set(['/'])

export default defineNuxtRouteMiddleware((to) => {
  if (import.meta.server) {
    return
  }

  const auth = useAuth()
  auth.restore()

  if (publicPaths.has(to.path)) {
    return
  }

  if (!auth.isAuthenticated.value) {
    const query: Record<string, string> = { returnTo: to.fullPath }
    if (auth.sessionExpiredNotice.value) {
      query.expired = '1'
    }

    return navigateTo({ path: '/', query })
  }
})
