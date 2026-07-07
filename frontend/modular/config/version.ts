export const garmetixModularVersion = {
  version: '6.0.57',
  stage: 'Stage 14I HR Module UX Overhaul',
  label: 'Version6 Stage 14I HR Module UX Overhaul',
  summary: 'HR: Employees route renamed to /employees; Employee/Attendance forms moved to slideover/modal with legacy row shape (masked mobile), ID card generation+print, and pagination on Employees/Attendance/Monthly. Monthly Attendance generation ported legacy\'s month-end auto-trigger. Payroll Summary rebuilt as a readable per-employee report. Regularization, Shifts, Shift Rules, Policies, Benefits, Payroll (Salary Structure/Payment), Salary Payment, and Salary Draft all converted to modal/slideover forms with readable labels, filters and pagination. Added a DELETE endpoint for Attendance Policies and View/Edit/Delete for Salary Payments. Also split the Books Notes menu out of Accounting into its own group, fixed the factory-reset endpoint to require SuperAdmin, and added an --apps= selective-deploy flag to the SRP deploy script.'
} as const
