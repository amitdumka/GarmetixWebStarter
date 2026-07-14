import { browserErrorDetails, persistClientMessageLog } from '~/utils/applicationMessageLog'

export default defineNuxtPlugin((nuxtApp) => {
  const previousHandler = nuxtApp.vueApp.config.errorHandler
  const originalConsoleError = console.error.bind(console)
  const originalConsoleWarn = console.warn.bind(console)

  function consoleArgument(value: unknown) {
    if (value instanceof Error) {
      return {
        name: value.name,
        message: value.message,
        stack: value.stack
      }
    }

    if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean' || value == null) {
      return value
    }

    try {
      return JSON.parse(JSON.stringify(value))
    } catch {
      return String(value)
    }
  }

  function persistConsoleMessage(level: 'Error' | 'Warning', eventName: string, args: unknown[]) {
    const message = args
      .map((value) => {
        if (value instanceof Error) return value.message
        if (typeof value === 'string') return value

        try {
          return JSON.stringify(value)
        } catch {
          return String(value)
        }
      })
      .filter(Boolean)
      .join(' ')
      .slice(0, 2000)

    void persistClientMessageLog({
      level,
      eventName,
      message: message || `Browser console ${level.toLowerCase()} recorded.`,
      detailsJson: browserErrorDetails(args.find(value => value instanceof Error), {
        arguments: args.map(consoleArgument)
      }),
      resource: window.location.pathname,
      success: false
    })
  }

  console.error = (...args: unknown[]) => {
    originalConsoleError(...args)
    persistConsoleMessage('Error', 'BrowserConsoleError', args)
  }

  console.warn = (...args: unknown[]) => {
    originalConsoleWarn(...args)
    persistConsoleMessage('Warning', 'BrowserConsoleWarning', args)
  }

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
