import { clearStoredSession, getAuthSessionSnapshot } from '@garmetix/shared-auth'
import { isAdminSession } from '../utils/admin-api'

const publicRoutes = new Set(['/login', '/access-denied'])

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

  if (snapshot.hasToken && !publicRoutes.has(to.path) && !isAdminSession(snapshot.user)) {
    return navigateTo('/access-denied')
  }

  if (snapshot.hasToken && to.path === '/login') {
    const redirect = typeof to.query.redirect === 'string' ? to.query.redirect : '/'
    return navigateTo(redirect.startsWith('/login') ? '/' : redirect)
  }
})
