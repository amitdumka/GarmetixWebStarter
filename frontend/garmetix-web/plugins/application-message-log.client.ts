import { browserErrorDetails, persistClientMessageLog } from '~/utils/applicationMessageLog'

export default defineNuxtPlugin((nuxtApp) => {
  const previousHandler = nuxtApp.vueApp.config.errorHandler

  nuxtApp.vueApp.config.errorHandler = (error, instance, info) => {
    void persistClientMessageLog({
      level: 'Error',
      eventName: 'VueError',
      message: error instanceof Error ? error.message : 'Unhandled Vue application error.',
      detailsJson: browserErrorDetails(error, { info }),
      resource: window.location.pathname,
      success: false
    })

    previousHandler?.(error, instance, info)
  }

  window.addEventListener('error', (event) => {
    void persistClientMessageLog({
      level: 'Error',
      eventName: 'BrowserError',
      message: event.message || 'Unhandled browser error.',
      detailsJson: browserErrorDetails(event.error, {
        filename: event.filename,
        line: event.lineno,
        column: event.colno
      }),
      resource: window.location.pathname,
      success: false
    })
  })

  window.addEventListener('unhandledrejection', (event) => {
    void persistClientMessageLog({
      level: 'Error',
      eventName: 'UnhandledPromiseRejection',
      message: event.reason?.message || 'Unhandled promise rejection.',
      detailsJson: browserErrorDetails(event.reason),
      resource: window.location.pathname,
      success: false
    })
  })
})
