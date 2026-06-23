export function formatIndianMoney(value: number | string | null | undefined, currency = 'INR') {
  const amount = Number(value || 0)
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency,
    maximumFractionDigits: 2
  }).format(Number.isFinite(amount) ? amount : 0)
}

export function stripServerUrl(value: string) {
  return String(value || '')
    .replace(/https?:\/\/[^\s"'<>),]+/gi, 'server')
    .replace(/\blocalhost:\d+\b/gi, 'server')
    .replace(/\b127\.0\.0\.1:\d+\b/gi, 'server')
    .trim()
}

