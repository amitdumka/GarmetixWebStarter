# Stage 8I Package 4 - Seeder Merge + Default Accounting Wins (v4.9.3)

This package updates the portable/AFSS seeder rules based on the live business structure.

## Business seed structure

- **Aadwika Fashion - Amit Kumar** and **Smart Menswear** now merge into one company:
  - Company: `Aadwika Fashion`
  - Store Group: `Aadwika Fashion MBO`
  - Stores:
    - `Aadwika Fashion MBO Dumka`
    - `Smart Menswear`
- **Aadwika Fashion - Shalini** remains separate:
  - Company: `Aadwika Fashion - Shalini`

## Default accounting wins

When a portable seeder file contains ledger/ledger group rows that clash with the protected Indian accounting defaults:

- The system default ledger/group wins.
- Export removes protected default ledger groups and ledgers from portable JSON data.
- Import skips protected default ledger/group rows.
- Import reapplies default accounting masters after importing.
- Custom ledgers that pointed to a skipped protected group are remapped to the recreated system default group.

## Why

This prevents duplicate/default-account clashes after crash recovery or migration while keeping user-created/custom accounting masters.
