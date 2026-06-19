# Workspace Selector v4.9.7

## How to change active store

1. Click the store/company pill in the top bar.
2. Select Company.
3. Select Store Group.
4. Select Store.
5. Click **Use workspace**.

## To keep it after refresh

Click **Set as default** in the workspace modal.

The selection is saved per user on the same browser using local storage. Admin users with multiple stores should not be reset to first store after page refresh.

## Test

1. Login as Admin.
2. Select second store, for example Smart Menswear.
3. Open Billing or Purchase.
4. Refresh browser.
5. Confirm same store remains selected.
6. Logout/login and confirm default store is restored on same browser.
7. On mobile/small screen, click top bar store pill and confirm selector opens.
