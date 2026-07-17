import { clearStoredSession, getAuthSessionSnapshot } from '@garmetix/shared-auth'

const publicRoutes = new Set(['/login', '/access-denied'])

function isOwnerSession(snapshot: ReturnType<typeof getAuthSessionSnapshot>) {
  return String(snapshot.user?.userType || '').toLowerCase() === 'owner'
}

export default defineNuxtRouteMiddleware((to) => {
  if (!import.meta.client) return

  const snapshot = getAuthSessionSnapshot(window.localStorage)

  if (!snapshot.hasToken && !publicRoutes.has(to.path)) {
    if (snapshot.label === 'Session expired') clearStoredSession(window.localStorage)
    return navigateTo({ path: '/login', query: { redirect: to.fullPath } })
  }

  // Strictly Owner-only, matching the backend's SwalekhaOwner policy - SuperAdmin and Admin
  // are refused here too, with no fallback. This is a UX guard only; the real gate is the
  // backend policy (AccessPermissionMatrix.IsOwner), which the frontend cannot bypass.
  if (snapshot.hasToken && !publicRoutes.has(to.path) && !isOwnerSession(snapshot)) {
    return navigateTo('/access-denied')
  }

  if (snapshot.hasToken && isOwnerSession(snapshot) && to.path === '/login') {
    const redirect = typeof to.query.redirect === 'string' ? to.query.redirect : '/'
    return navigateTo(redirect.startsWith('/login') ? '/' : redirect)
  }
})
