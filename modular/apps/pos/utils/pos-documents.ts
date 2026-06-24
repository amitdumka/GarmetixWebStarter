import { createApiUrl } from '@garmetix/shared-api'

export interface BillingPdfOptions {
  apiBaseUrl: string
  invoiceId: string
  token: string | null
  reprint?: boolean
  format?: 'a4' | 'a5' | 'thermal-2' | 'thermal-3'
  copy?: 'customer' | 'office'
  signatures?: boolean
}

export interface PettyCashPdfOptions {
  apiBaseUrl: string
  pettyCashSheetId: string
  token: string | null
}

export async function openBillingInvoicePdf(options: BillingPdfOptions) {
  if (!options.invoiceId) throw new Error('Invoice reference is missing.')

  const query = new URLSearchParams({
    format: options.format || 'a4',
    copy: options.copy || 'customer',
    reprint: String(options.reprint ?? false),
    signatures: String(options.signatures ?? true)
  })
  await openApiPdf({
    apiBaseUrl: options.apiBaseUrl,
    path: `billing/sales/${options.invoiceId}/pdf?${query.toString()}`,
    token: options.token,
    missingMessage: 'Could not load invoice PDF.',
    blockedMessage: 'PDF was generated, but the browser blocked the print window. Use Print Queue to retry.'
  })
}

export async function openPettyCashSheetPdf(options: PettyCashPdfOptions) {
  if (!options.pettyCashSheetId) throw new Error('Petty cash sheet reference is missing.')

  await openApiPdf({
    apiBaseUrl: options.apiBaseUrl,
    path: `petty-cash-sheets/${options.pettyCashSheetId}/pdf`,
    token: options.token,
    missingMessage: 'Could not load petty cash PDF.',
    blockedMessage: 'Petty cash PDF was generated, but the browser blocked the print window. Use Print Petty Cash to retry.'
  })
}

async function openApiPdf(options: {
  apiBaseUrl: string
  path: string
  token: string | null
  missingMessage: string
  blockedMessage: string
}) {
  const response = await fetch(createApiUrl(options.apiBaseUrl, options.path), {
    headers: options.token ? { Authorization: `Bearer ${options.token}` } : undefined
  })

  if (!response.ok) {
    throw new Error(response.status === 401 || response.status === 403
      ? 'Login is required before opening this PDF.'
      : options.missingMessage)
  }

  const blob = await response.blob()
  const blobUrl = URL.createObjectURL(blob)
  const opened = window.open(blobUrl, '_blank', 'noopener,noreferrer')
  if (!opened) {
    setTimeout(() => URL.revokeObjectURL(blobUrl), 1000)
    throw new Error(options.blockedMessage)
  }

  setTimeout(() => URL.revokeObjectURL(blobUrl), 60_000)
}

export function normalizePosDocumentSearch(value: string | null | undefined) {
  const raw = String(value || '').trim()
  if (!raw) return ''

  const jsonValue = parseJsonReference(raw)
  if (jsonValue) return jsonValue

  const urlValue = parseUrlReference(raw)
  if (urlValue) return urlValue

  return raw
    .replace(/^(invoice|inv|bill|qr|ref|reference)\s*[:#-]\s*/i, '')
    .replace(/^["']|["']$/g, '')
    .trim()
}

export function textMatchesDocumentSearch(value: string | null | undefined, search: string) {
  const normalizedValue = String(value || '').trim().toLowerCase()
  const normalizedSearch = normalizePosDocumentSearch(search).toLowerCase()
  return Boolean(normalizedSearch && normalizedValue.includes(normalizedSearch))
}

function parseJsonReference(raw: string) {
  if (!raw.startsWith('{')) return ''
  try {
    const data = JSON.parse(raw)
    return String(data.invoiceNumber || data.invoiceId || data.id || data.number || data.reference || '').trim()
  } catch {
    return ''
  }
}

function parseUrlReference(raw: string) {
  try {
    const url = new URL(raw)
    for (const key of ['invoiceNumber', 'invoiceId', 'id', 'number', 'ref', 'q']) {
      const value = url.searchParams.get(key)
      if (value) return value.trim()
    }

    const pathValue = url.pathname
      .split('/')
      .map(part => decodeURIComponent(part.trim()))
      .filter(Boolean)
      .at(-1)
    return pathValue || ''
  } catch {
    return ''
  }
}
