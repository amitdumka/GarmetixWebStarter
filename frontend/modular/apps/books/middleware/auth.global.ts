import { clearStoredSession, getAuthSessionSnapshot } from '@garmetix/shared-auth'

const publicRoutes = new Set(['/login'])

export default defineNuxtRouteMiddleware((to) => {
  if (!import.meta.client) return

  const snapshot = getAuthSessionSnapshot(window.localStorage)
  if (!snapshot.hasToken && !publicRoutes.has(to.path)) {
    if (snapshot.label === 'Session expired') clearStoredSession(window.localStorage)
    return navigateTo({
      path: '/login',
      query: { redirect: to.fullPath }
    })
  }

  if (snapshot.hasToken && to.path === '/login') {
    const redirect = typeof to.query.redirect === 'string' ? to.query.redirect : '/'
    return navigateTo(redirect.startsWith('/login') ? '/' : redirect)
  }
})
