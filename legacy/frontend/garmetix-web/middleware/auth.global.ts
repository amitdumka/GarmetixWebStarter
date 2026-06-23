const publicPaths = new Set(['/access-denied'])

export default defineNuxtRouteMiddleware((to) => {
  if (import.meta.server) {
    return
  }

  const auth = useAuth()
  auth.restore()

  if (to.path === '/') {
    if (!auth.isAuthenticated.value) {
      return
    }

    if (!auth.canSeeAdmin.value) {
      return navigateTo('/dashboard')
    }

    return
  }

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

  const access = useAccessControl()
  const decision = access.checkPath(to.path)
  if (!decision.allowed) {
    return navigateTo({
      path: '/access-denied',
      query: {
        path: to.fullPath,
        reason: decision.reason
      }
    })
  }
})
