export function useAttendanceShifts() {
  const api = useGarmetixApi()
  const shifts = () => api.list<any>('attendance/shifts')
  const createShift = (body: any) => api.create<any>('attendance/shifts', body)
  const updateShift = (id: string, body: any) => api.update<any>('attendance/shifts', id, body)
  const deleteShift = (id: string) => api.remove('attendance/shifts', id)
  const policies = () => api.list<any>('attendance/policies')
  const createPolicy = (body: any) => api.create<any>('attendance/policies', body)
  const updatePolicy = (id: string, body: any) => api.update<any>('attendance/policies', id, body)
  return { shifts, createShift, updateShift, deleteShift, policies, createPolicy, updatePolicy }
}
