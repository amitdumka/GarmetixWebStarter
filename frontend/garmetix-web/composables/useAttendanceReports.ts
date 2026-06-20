export function useAttendanceReports() {
  const attendance = useAttendance()
  return {
    monthly: attendance.monthly,
    payrollSummary: attendance.payrollSummary,
    payrollReview: attendance.payrollReview,
    rebuildPayrollReview: attendance.rebuildPayrollReview,
    markPayrollReview: attendance.markPayrollReview,
    salarySlipDrafts: attendance.salarySlipDrafts,
    rebuildSalarySlipDrafts: attendance.rebuildSalarySlipDrafts,
    markSalarySlipDraft: attendance.markSalarySlipDraft,
    generateSalarySlipsFromDrafts: attendance.generateSalarySlipsFromDrafts,
    salaryPaymentCandidates: attendance.salaryPaymentCandidates,
    generateSalaryPaymentsFromDrafts: attendance.generateSalaryPaymentsFromDrafts,
    deviceBridgeStatus: attendance.deviceBridgeStatus,
    finalAcceptance: attendance.finalAcceptance,
    recalculate: attendance.recalculate,
    lockMonth: attendance.lockMonth
  }
}
