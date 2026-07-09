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

const INACTIVE_EMPLOYEE_STATUSES = new Set(['resigned', 'terminated', 'inactive'])

/**
 * True when an employee record should appear in a picker used to attach a new
 * record (voucher "issued by", attendance punch, salary payment, etc.) - working
 * and not in a terminal status. This was duplicated three times across the HR app
 * with slightly different logic (one used OR instead of AND); this is the single
 * source of truth now.
 */
export function isActiveEmployee(employee: Record<string, unknown> | null | undefined): boolean {
  if (!employee) return false
  const workingRaw = employee.working ?? employee.Working
  const isWorking = workingRaw === true || String(workingRaw ?? '').toLowerCase() === 'true'
  const status = String(employee.employeeStatus ?? employee.EmployeeStatus ?? employee.status ?? '').toLowerCase()
  return isWorking && !INACTIVE_EMPLOYEE_STATUSES.has(status)
}

