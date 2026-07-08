export const garmetixModularVersion = {
  version: '6.0.60',
  stage: 'Stage 14K.1 HR Attendance Menu Fix',
  label: 'Version6 Stage 14K.1 HR Attendance Menu Fix',
  summary: 'HR: the sidebar Attendance submenu (a hardcoded localMenus list in ModularAppShell.vue, separate from routes.ts) had drifted out of sync with real routes - Attendance Dashboard, Manual Punch, Shifts, Shift Rules and Policies all had working pages/routes but no sidebar entry, so they were only reachable by typing the URL directly. Added all five. Same root-cause class as the earlier Books Notes/GST menu-split bug: routes.ts having a route does not mean the sidebar shows it, since the sidebar reads from this separate hardcoded list.'
} as const
