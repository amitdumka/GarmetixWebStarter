export const APP_VERSION = '4.9.24'
export const APP_STAGE = 'Stage 8I Package 23B Employee Save Hotfix'
export const APP_RELEASE_NAME = 'Employee Save Hotfix'
export const APP_BUILD_DATE = '2026-06-19'
export const APP_BUILD_CODE = 'GARMETIX-8I-20260619-49240'

export const APP_HIGHLIGHTS = [
  'Employee save now avoids EF Core DefaultIfEmpty translation failures during PUT /api/employees.',
  'Deprecated lucide-vue-next dependency removed; Nuxt UI lucide icons remain through @iconify-json/lucide.',
  'HR schema repair now auto-adds missing Package 22/23 employee columns and the EmployeePayrollAdjustments table on existing PostgreSQL volumes.',
  'HR Employee Master now supports auto employee code, photo preview, document/bank fields, lifecycle status and printable ID-card readiness.',
  'HR Benefits adds salary advance, advance recovery, leave, bonus, leave encashment, PF and gratuity adjustment workflow for payroll.',
  'Payroll generation and payment preview now consider HR Benefits adjustments such as advances, bonus, PF and gratuity rows.',
  'License Activation now has a dedicated admin page for signed client license generation, activation and status review.',
  'The API includes HMAC-signed offline license keys, masked license status and optional operational API enforcement.',
  'Docker/env templates now include LICENSE_* settings plus a persistent /app/license activation volume.',
  'A host-level license acceptance drill verifies status, optional activation and protected API blocking behavior.',
  'Test Automation now includes LICENSE_SAAS_ACTIVATION so licensing remains part of production readiness.',
  'SMTP, print/PDF, backup/restore, permission, secret hygiene and Docker acceptance checks remain in the release manifest.'
]
