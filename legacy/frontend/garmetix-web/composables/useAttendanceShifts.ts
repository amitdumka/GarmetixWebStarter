export function useAttendanceShifts() {
  const api = useGarmetixApi()
  const shifts = () => api.list<any>('attendance/shifts')
  const createShift = (body: any) => api.create<any>('attendance/shifts', body)
  const updateShift = (id: string, body: any) => api.update<any>('attendance/shifts', id, body)
  const deleteShift = (id: string) => api.remove('attendance/shifts', id)
  const shiftRules = () => api.list<any>('attendance/shift-rules')
  const createShiftRule = (body: any) => api.create<any>('attendance/shift-rules', body)
  const updateShiftRule = (id: string, body: any) => api.update<any>('attendance/shift-rules', id, body)
  const deleteShiftRule = (id: string) => api.remove('attendance/shift-rules', id)
  const policies = () => api.list<any>('attendance/policies')
  const createPolicy = (body: any) => api.create<any>('attendance/policies', body)
  const updatePolicy = (id: string, body: any) => api.update<any>('attendance/policies', id, body)
  return { shifts, createShift, updateShift, deleteShift, shiftRules, createShiftRule, updateShiftRule, deleteShiftRule, policies, createPolicy, updatePolicy }
}
