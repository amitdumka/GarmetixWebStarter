# Portable JSON Seeder v4.9.2 Operations

## Export current system as JSON seeder

Open:

```text
Admin → AF/SS Seeder
```

Click:

```text
Create seeder file from current data
```

Store the downloaded JSON file with your backup files.

## Import JSON seeder on new/crashed system

1. Deploy Garmetix.
2. Login as Admin.
3. Open **Admin → AF/SS Seeder**.
4. Use **Import JSON seeder**.
5. Run **Data → Data Consistency**.
6. Verify users, company, store, accounting, inventory and GST data.

## Create company with AF/SS profile

1. Open **Admin → AF/SS Seeder**.
2. Keep **Create company/store group/store automatically** checked.
3. Select AF/SS profile.
4. Confirm and run seed.

## Protected defaults

Default Indian accounting ledgers and ledger groups cannot be deleted from normal CRUD delete.
Create corrective/extra ledgers instead of deleting defaults.

## Sensitive warning

Portable JSON seeder files can contain business data and password hashes. Keep them private like a database backup. Do not send them on WhatsApp/email unless encrypted.
