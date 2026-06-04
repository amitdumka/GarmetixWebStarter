# Auth And Access

The API now has JWT authentication and role-based authorization policies.

## First Admin

After the database is created, call this endpoint once:

```http
POST /api/auth/bootstrap-admin
```

Example body:

```json
{
  "name": "Garmetix Admin",
  "userName": "admin",
  "email": "admin@garmetix.local",
  "password": "change-me"
}
```

The endpoint works only while no admin user exists. It creates an `Admin` user and returns a JWT.

## Login

```http
POST /api/auth/login
```

Example body:

```json
{
  "userName": "admin",
  "password": "change-me"
}
```

Use the returned token:

```http
Authorization: Bearer <token>
```

## Policies

- `CompanySetup`: `Admin`, `PowerUser`
- `Billing`: `Admin`, `PowerUser`, `StoreManager`, `Salesman`
- `Inventory`: `Admin`, `PowerUser`, `StoreManager`
- `Purchase`: `Admin`, `PowerUser`, `StoreManager`
- `Accounting`: `Admin`, `PowerUser`, `Accountant`, `RemoteAccountant`
- `Hr`: `Admin`, `PowerUser`, `StoreManager`
- `Payroll`: `Admin`, `PowerUser`, `Accountant`
- `Admin`: `Admin`, `PowerUser`

Passwords are stored as PBKDF2 hashes. Legacy plain-text passwords are accepted once and upgraded after a successful login.

## First-Run Setup

After login, call:

```http
GET /api/setup/status
```

If company/store/product defaults are missing, call:

```http
POST /api/setup/quick-start
```

This creates a company, store group, store, default product category, default product subcategory, and `GST 5` tax record.
