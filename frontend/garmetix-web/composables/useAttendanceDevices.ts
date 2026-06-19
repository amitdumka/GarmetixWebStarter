export function useAttendanceDevices() {
  const api = useGarmetixApi()
  const list = () => api.list<any>('attendance/devices')
  const register = (body: any) => api.create<any>('attendance/devices/register', body)
  const revoke = (id: string) => api.create<any>(`attendance/devices/${id}/revoke`, {})
  const heartbeat = (body: any) => api.create<any>('attendance/devices/heartbeat', body)
  const kioskBootstrap = (body: any = {}) => api.create<any>('attendance/kiosk/bootstrap', body)
  const kioskReadiness = (body: any) => api.create<any>('attendance/kiosk/readiness', body)
  const kioskLookupEmployee = (body: any) => api.create<any>('attendance/kiosk/lookup-employee', body)
  const uploadPhotoProof = (body: any) => api.create<any>('attendance/kiosk/photo-proof', body)
  const kioskPunch = (body: any) => api.create<any>('attendance/kiosk/punch', body)
  const syncPending = (body: any) => api.create<any>('attendance/kiosk/sync-pending', body)
  const photoProofs = (status?: string) => api.list<any>(status ? `attendance/photo-proofs?status=${encodeURIComponent(status)}` : 'attendance/photo-proofs')
  const photoProofReviewSummary = () => api.list<any>('attendance/photo-proofs/review-summary')
  const reviewPhotoProof = (id: string, body: any) => api.create<any>(`attendance/photo-proofs/${id}/review`, body)
  const createPhotoProofRegularization = (id: string, remarks: string) => api.create<any>(`attendance/photo-proofs/${id}/regularization`, { remarks })
  const syncBatches = () => api.list<any>('attendance/sync-batches')
  return { list, register, revoke, heartbeat, kioskBootstrap, kioskReadiness, kioskLookupEmployee, uploadPhotoProof, kioskPunch, syncPending, photoProofs, photoProofReviewSummary, reviewPhotoProof, createPhotoProofRegularization, syncBatches }
}
