# Nexus Communication - complete update (all remaining fixes)

## First login
Default Admin: **admin@nexus.com / Admin@123** (change it).  
Admin creates employee logins on *Employees > Add*.  
Public Register creates **Customer** accounts only.

## Database migration required
New columns / tables:
- `Customers.CustomerCode` (nvarchar(10), unique)
- `UsageRecords` table

Run in Visual Studio Package Manager Console:
```
Add-Migration AddCustomerCodeAndUsageRecords
Update-Database
```
Or apply the SQL in `Scripts/AddCustomerCodeAndUsage.sql` against NexusDB.

Existing customers get empty CustomerCode until you re-save or backfill:
```sql
-- optional backfill
;WITH n AS (SELECT Id, ROW_NUMBER() OVER (ORDER BY Id) AS rn FROM Customers WHERE CustomerCode IS NULL OR CustomerCode = '')
UPDATE c SET CustomerCode = RIGHT('0000000000' + CAST(n.rn AS varchar(10)), 10)
FROM Customers c JOIN n ON c.Id = n.Id;
```

## Fixes applied (items 1–10)

### 1. Plans visible to Retail / Technical / Accounts / Customer
- `PlansController` Index + Details allowed for those roles (Create/Edit/Delete still Admin only).
- Navbar: staff “Plans” link + Customer “Browse Plans”.
- Plans Index hides Add/Edit/Delete for non-Admin.

### 2. Customer contact details
- `MyPortal/Profile` and `MyPortal/EditProfile` – view and edit name, phone, address (email locked).
- Menu: My Profile.

### 3. Bill → automatic connection status
- After bill generate and after payment: if any unpaid bill is past DueDate → connection becomes TemporarilyInactive (history reason “Automatic: …”).
- When all overdue cleared → auto-reactivate (only if last status change was automatic).

### 4. Previous due joins bill total
- `PreviousDue` is added into `TotalAmount` / `BalanceDue` and shown as a line item “Previous outstanding balance”.

### 5. 10-digit Customer Code
- `Customer.CustomerCode` generated via SequenceHelper on create (0000000001 …).
- Shown on Customers Index and My Profile.

### 6. “Who did it” employee ids
- Orders: `PlacedByEmployeeId`
- Feasibility: `CheckedByEmployeeId`
- Connections: `CreatedByEmployeeId`
- Status / plan change history: `ChangedByEmployeeId`
- Bills: `GeneratedByEmployeeId`
- Payments: `ReceivedByEmployeeId`
Resolved from logged-in employee email via `CurrentEmployee.GetIdAsync`.

### 7. Equipment demand report
- `Reports/EquipmentDemand` – pending order equipment vs stock, shortfall highlighted.
- Linked from Reports menu when present.

### 8. Hourly usage tracking
- `UsageRecord` model + `Connections/RecordUsage` action + form on Connection Details.
- Unbilled extra hours (beyond plan Hours) are charged on the next bill.

### 9. Security
- `Orders/Details`: Customer may only open own orders (Forbid otherwise).
- Order Create dropdown: Customer only sees own profile (not all customers).

### 10. Number clash
- `SequenceHelper` uses `SequenceCounters` table with row-level update in a transaction for Order / Account / Bill / Receipt / Customer / PO numbers.

## Still note
- Build in Visual Studio (SDK not present in this environment).
- After migration, first run will seed sequences as needed.

## Portable migration (any PC)
Migration `20261021000000_AddCustomerCodeAndUsageRecords` backfills CustomerCode **before** creating the unique index, so it works on:
- empty database
- database that already has customers with empty codes
- second PC after copying the project

On any machine: `Update-Database` only.

If an older failed migration id is in `__EFMigrationsHistory`, delete that row or `Remove-Migration` first.
Startup also backfills empty codes as a safety net.

## UI update (latest)
- All Add / New / Place Order / Record Payment / Generate Bill / Upload buttons are now at the right end of the page header.
- Every Details / Edit / Delete link in table "Actions" columns (also Plans, Customers, Products ...) shows as the same icon buttons in one row.


Connection string

{
  "ConnectionStrings": {
    "DefaultConnection": "Server=ASIM\\SQLEXPRESS;Database=NexusDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}


Connection string 2

{
  "ConnectionStrings": {
    "DefaultConnection": "Server=LAB1-8;Database=NexusDB;User Id=sa;Password=aptech;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
