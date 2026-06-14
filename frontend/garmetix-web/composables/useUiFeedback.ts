import { persistClientMessageLog, redactSensitiveDetails } from '~/utils/applicationMessageLog'

type ToastColor = 'success' | 'error' | 'warning' | 'info' | 'neutral'

type UiLogEntry = {
  id: string
  at: string
  title: string
  message: string
  details?: string
  color: ToastColor
  statusCode?: number
  resource?: string
}

export function useUiFeedback() {
  const toast = useToast()
  const logs = useState<UiLogEntry[]>('garmetix.ui.logs', () => [])

  function notify(title: string, description?: string, color: ToastColor = 'success') {
    const cleanTitle = sanitizeMessage(title)
    const cleanDescription = description ? sanitizeMessage(description) : undefined

    toast.add({
      title: cleanTitle,
      description: cleanDescription,
      color
    })

    addLog(cleanTitle, cleanDescription || '', undefined, color)
  }

  function saved(entity = 'Record') {
    notify(`${entity} saved`)
  }

  function updated(entity = 'Record') {
    notify(`${entity} updated`)
  }

  function deleted(entity = 'Record') {
    notify(`${entity} deleted`, undefined, 'warning')
  }

  function failed(title = 'Action failed', error?: unknown) {
    const parsed = parseError(error)
    const message = parsed.message || 'Please check the message log for details.'

    toast.add({
      title: sanitizeMessage(title),
      description: parsed.hasDetails
        ? `${sanitizeMessage(message)} Details saved in Message Log.`
        : sanitizeMessage(message),
      color: 'error'
    })

    addLog(title, message, parsed.details, 'error', parsed.statusCode, parsed.resource)
  }

  function errorMessage(error: unknown, fallback = 'Action failed.', title = 'Action failed') {
    const parsed = parseError(error)
    const message = parsed.message || fallback
    addLog(title, message, parsed.details, 'error', parsed.statusCode, parsed.resource)
    return sanitizeMessage(message)
  }

  function cleanMessage(value: string) {
    return sanitizeMessage(value)
  }

  function addLog(
    title: string,
    message: string,
    details?: string,
    color: ToastColor = 'info',
    statusCode?: number,
    resource?: string
  ) {
    const entry = {
      id: `${Date.now()}-${Math.random().toString(16).slice(2)}`,
      at: new Date().toISOString(),
      title: sanitizeMessage(title),
      message: sanitizeMessage(message),
      details: details ? sanitizeDetails(details) : undefined,
      color,
      statusCode,
      resource: resource ? sanitizeMessage(resource) : undefined
    }

    logs.value = [
      entry,
      ...logs.value
    ].slice(0, 100)

    void persistClientMessageLog({
      level: logLevel(color),
      eventName: entry.title || 'UI Message',
      message: entry.message || entry.title || 'UI message recorded.',
      detailsJson: entry.details,
      resource: entry.resource,
      success: color !== 'error' && color !== 'warning'
    })
  }

  function clearLogs() {
    logs.value = []
  }

  function parseError(error?: unknown) {
    const raw = error as any
    const statusCode = raw?.statusCode || raw?.status || raw?.response?.status
    const data = raw?.data || raw?.response?._data
    const validationMessage = validationSummary(data)
    const request = raw?.garmetixRequest
    const message =
      validationMessage ||
      data?.message ||
      data?.title ||
      raw?.statusMessage ||
      friendlyStatusMessage(statusCode) ||
      raw?.message ||
      'Request failed.'

    const details = safeStringify({
      statusCode,
      statusMessage: raw?.statusMessage,
      message: raw?.message,
      request,
      data,
      validationErrors: extractValidationErrors(data)
    })

    return {
      message: sanitizeMessage(String(message || '')),
      details,
      hasDetails: Boolean(details && details !== '{}'),
      statusCode: typeof statusCode === 'number' ? statusCode : Number(statusCode) || undefined,
      resource: request?.resource
    }
  }

  function friendlyStatusMessage(statusCode?: number | string) {
    const code = Number(statusCode)
    if (!code) {
      return ''
    }

    if (code === 400) {
      return 'The submitted data could not be saved.'
    }

    if (code === 401) {
      return 'Your session has expired. Please login again.'
    }

    if (code === 403) {
      return 'You do not have permission for this action.'
    }

    if (code === 404) {
      return 'The requested record was not found.'
    }

    if (code === 409) {
      return 'This record conflicts with existing data.'
    }

    if (code >= 500) {
      return 'Server error. Please check Message Log.'
    }

    return `Request failed with status ${code}.`
  }

  function validationSummary(data: any) {
    const errors = extractValidationErrors(data)
    if (!errors.length) {
      return ''
    }

    return `${errors.length} validation issue${errors.length === 1 ? '' : 's'} found.`
  }

  function extractValidationErrors(data: any) {
    if (!data?.errors || typeof data.errors !== 'object') {
      return []
    }

    return Object.entries(data.errors).flatMap(([field, value]) => {
      const messages = Array.isArray(value) ? value : [value]
      return messages.map((message) => ({
        field,
        message: String(message)
      }))
    })
  }

  function sanitizeMessage(value: string) {
    return String(value || '')
      .replace(/https?:\/\/[^\s"'<>),]+/gi, 'server')
      .replace(/\blocalhost:\d+\b/gi, 'server')
      .replace(/\b127\.0\.0\.1:\d+\b/gi, 'server')
      .replace(/\bhost\.docker\.internal:\d+\b/gi, 'server')
      .replace(/\[([A-Z]+)\]\s+server\/api\/?/gi, '$1 request failed')
      .trim()
  }

  function sanitizeDetails(value: string) {
    return redactSensitiveDetails(sanitizeMessage(value))
  }

  function logLevel(color: ToastColor) {
    if (color === 'error') return 'Error'
    if (color === 'warning') return 'Warning'
    if (color === 'success') return 'Success'
    return 'Info'
  }

  function safeStringify(value: unknown) {
    try {
      return JSON.stringify(value, null, 2)
    } catch {
      return String(value || '')
    }
  }

  return {
    logs,
    notify,
    saved,
    updated,
    deleted,
    failed,
    errorMessage,
    cleanMessage,
    addLog,
    clearLogs
  }
}
