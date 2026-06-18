# Customer, Debit/Credit Note, and Loyalty Refinement

This update separates commercial note entry into dedicated module routes and adds a real Customer module.

## Frontend routes

- `/debit-notes` - debit note register
- `/debit-notes/new` - debit note entry form
- `/debit-notes/{id}` - debit note edit form
- `/credit-notes` - credit note register
- `/credit-notes/new` - credit note entry form
- `/credit-notes/{id}` - credit note edit form
- `/commercial-notes` - commercial note summary/register only
- `/customers` - customer module with GST, credit, and loyalty overview
- `/customers/new` - customer entry form
- `/customers/{id}` - customer edit form
- `/loyalty` - store loyalty setup and customer point adjustment/ledger

## Backend changes

- `PUT /api/commercial-notes/{id}` edits only manual notes.
- `GET /api/loyalty/customers/{customerId}` returns loyalty summary.
- `POST /api/loyalty/customers/{customerId}/adjust` manually adds/redeems loyalty points with ledger entry.
- Audit, commercial notes, customer advances, and loyalty endpoints now trigger defensive schema repair before querying newer tables.
- `POST /api/database/repair` allows an Admin to manually run the idempotent schema repair service.

## Behaviour

- Debit Note and Credit Note are no longer entered on the same form.
- Commercial Notes is a summary page only.
- Customer module owns customer loyalty balance visibility.
- Loyalty page can still configure the store-level program and adjust/redeem customer points.
