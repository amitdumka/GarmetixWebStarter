# Seeder Merge + Default Accounting Wins v4.9.3

## AF/SS seeding

Use **Admin → AF/SS Seeder**.

- Select **Aadwika Fashion - Amit Kumar** to create/update company `Aadwika Fashion`, store group `Aadwika Fashion MBO`, store `Aadwika Fashion MBO Dumka`.
- Select **Smart Menswear** to create/update a second store under the same company/store group.
- Select **Aadwika Fashion - Shalini** to create/update the separate Shalini company/profile.

## Portable JSON

When exporting a portable JSON seeder:

- Protected system default ledgers/groups are not included.
- Custom ledgers/groups remain included.

When importing:

- Protected ledger/group clashes are skipped.
- Default accounting masters are recreated/reapplied.
- Custom ledger group references are remapped if they pointed to skipped protected groups.

After import, run:

```bash
Data → Data Consistency
```
