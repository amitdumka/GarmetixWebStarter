export function useAttendanceReports() {
  const attendance = useAttendance()
  return {
    monthly: attendance.monthly,
    payrollSummary: attendance.payrollSummary,
    payrollReview: attendance.payrollReview,
    rebuildPayrollReview: attendance.rebuildPayrollReview,
    markPayrollReview: attendance.markPayrollReview,
    recalculate: attendance.recalculate,
    lockMonth: attendance.lockMonth
  }
}
