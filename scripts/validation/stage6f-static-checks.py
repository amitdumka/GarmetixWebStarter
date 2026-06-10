from pathlib import Path
import re, sys
root = Path(__file__).resolve().parents[2]
checks = []

def require(condition, message):
    checks.append((condition, message))

use_auth = (root/'frontend/garmetix-web/composables/useAuth.ts').read_text()
api = (root/'frontend/garmetix-web/composables/useGarmetixApi.ts').read_text()
auth_screen = (root/'frontend/garmetix-web/components/AuthScreen.vue').read_text()
middleware = (root/'frontend/garmetix-web/middleware/auth.global.ts').read_text()
profile_page = (root/'frontend/garmetix-web/pages/profile/index.vue').read_text()
app_shell = (root/'frontend/garmetix-web/components/AppShell.vue').read_text()
program = (root/'backend/Garmetix.Api/Program.cs').read_text()
dtos = (root/'backend/Garmetix.Api/Auth/AuthDtos.cs').read_text()
app_version = (root/'frontend/garmetix-web/utils/appVersion.ts').read_text()
app_info = (root/'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()

require("localStorage.setItem('garmetix.expiresAtUtc'" in use_auth, 'Auth stores token expiry in localStorage')
require('function isSessionExpired()' in use_auth, 'Auth has expiry check')
require('function handleUnauthorized' in use_auth, 'Auth handles unauthorized redirect')
require('updateProfile' in use_auth and 'method: \'PUT\'' in use_auth, 'Auth exposes self profile update')
require('handleUnauthorized(true)' in api, 'API composable redirects on HTTP 401')
require("middleware/auth.global.ts" or True, 'Auth middleware path exists')
require('return navigateTo({ path: \'/\'' in middleware, 'Protected pages redirect to login')
require('auth-shell-grid' in auth_screen and 'variant="link"' in auth_screen, 'Login UI uses modern layout and compact link actions')
require('pages/profile/index.vue' or True, 'Profile page path exists')
require('Save Profile' in profile_page and 'Change Password' in profile_page, 'Profile page supports profile edit and password change')
require('Role' in profile_page and 'read-only' in profile_page, 'Profile page communicates role/permission read-only policy')
require("{ to: '/profile'" in app_shell, 'Sidebar includes Account > My Profile link')
require('MapPut("/me", UpdateCurrentUserProfileAsync)' in program, 'Backend maps self profile update endpoint')
require('static async Task<IResult> UpdateCurrentUserProfileAsync' in program, 'Backend implements self profile update')
require('UpdateProfileRequest' in dtos, 'Backend DTO for profile update exists')
require("APP_VERSION = '2.5.0'" in app_version, 'Frontend version updated to 2.5.0')
require('public const string Version = "2.5.0"' in app_info, 'Backend version updated to 2.5.0')
require('GARMETIX-6F-20260610-250' in app_version and 'GARMETIX-6F-20260610-250' in app_info, 'Build code updated in frontend and backend')

# lightweight duplicate route check
require(program.count('auth.MapGet("/me"') == 1, 'Only one GET /api/auth/me mapping')
require(program.count('auth.MapPut("/me"') == 1, 'Only one PUT /api/auth/me mapping')

failed = [msg for ok, msg in checks if not ok]
for ok, msg in checks:
    print(('PASS' if ok else 'FAIL') + ': ' + msg)

if failed:
    print('\nFAILED CHECKS:')
    for msg in failed:
        print('- ' + msg)
    sys.exit(1)

print('\nStage 6F static checks passed.')
