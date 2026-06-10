export const APP_VERSION = '2.5.0'
export const APP_STAGE = 'Stage 6F'
export const APP_RELEASE_NAME = 'Auth UX, Session Guard and User Profile'
export const APP_BUILD_DATE = '2026-06-10'
export const APP_BUILD_CODE = 'GARMETIX-6F-20260610-250'

export const APP_HIGHLIGHTS = [
  'Modern login screen with one primary Login button and compact forgot/reset links',
  'Global auth guard redirects expired or missing sessions to Login before protected pages render',
  'API 401 handling now clears expired sessions and returns the user to Login',
  'My Profile page with editable name, username, email and self-service password change',
  'Profile changes never allow users to modify their own role, permission or workspace assignment',
  'Company, store group and store onboarding module',
  'AF/SS default data seeder module',
  'Application message logs with filters',
  'Version identity surfaced in About Us and API',
  'Non-GST/out-of-scope goods purchase and sale module',
  'Buildfix: added missing Store namespace import for Docker publish',
  'Compile fix: resolved stock variable shadowing and ProductCategory ambiguity',
  'Multi-item Non-GST purchase memo and sale cash memo with print view',
  'Non-GST sale, purchase, profit and current stock reports',
  'Separate Non-GST stock flag and movement ledger',
  'Separate Non-GST goods reports and visible accounting postings',
  'About Us, Contact Us and FAQ pages',
  'Runtime fix: removed duplicate app-info and message-log root routes',
  'Runtime fix: Message Logs PostgreSQL filter query no longer uses untyped NULL parameters',
  'Runtime fix: Non-GST sale sends null stock id instead of blank string and purchase auto-generates barcode when blank'
]
