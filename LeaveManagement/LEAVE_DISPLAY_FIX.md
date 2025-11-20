# Leave Not Displaying After Creation - Diagnostic Guide

## Problem
Leave requests are created successfully but don't appear in "My Leaves" page.

## Enhanced Logging Added ✅

The code now includes detailed logging to help diagnose the issue:

### When Creating Leave:
- Logs the UserId used for creation
- Logs the UserId stored in the database
- Verifies the leave was saved by querying it back
- Logs verification results

### When Loading Leaves:
- Logs the UserId used for querying
- Logs how many leaves were found
- If no leaves found, logs all UserIds in the database (for comparison)
- Logs total count of leaves in database

## How to Diagnose

### Step 1: Check Application Insights Logs

After creating a leave, look for these log entries:

**On Create:**
```
Creating leave request for user {UserId}, StartDate: {StartDate}, EndDate: {EndDate}
Leave request saved successfully with ID: {Id}
Verification: Found {Count} leaves for user {UserId} after creation
```

**On Index Page:**
```
Loading leaves for user {UserId}
Querying leaves for UserId: '{UserId}' (Length: {Length})
Found {Count} leave requests for UserId: '{UserId}'
```

### Step 2: Check for UserId Mismatch

If you see this warning:
```
No leaves found for UserId '{UserId}'. Sample UserIds in database: ...
```

This means the UserId used to query doesn't match the UserId stored in the database.

**Common Causes:**
1. **Different claim types**: UserId extracted differently on create vs. query
2. **Whitespace**: Extra spaces in UserId
3. **Case sensitivity**: Different casing (though SQL Server is usually case-insensitive)
4. **Session issue**: Different user session

### Step 3: Verify Database Directly

Run this SQL query in Azure SQL Database:

```sql
-- Check all leaves and their UserIds
SELECT Id, UserId, StartDate, EndDate, Type, Status, CreatedAt
FROM LeaveRequests
ORDER BY CreatedAt DESC;

-- Check your specific UserId (replace with your actual UserId from logs)
SELECT Id, UserId, StartDate, EndDate, Type, Status
FROM LeaveRequests
WHERE UserId = 'YOUR_USER_ID_HERE';
```

## Quick Fixes

### Fix 1: Ensure Consistent UserId Extraction
The `ClaimsHelper.GetUserId()` should be used consistently. Verify both pages use it.

### Fix 2: Check for Whitespace
The code now trims UserId before querying. If you still have issues, check logs for UserId length differences.

### Fix 3: Verify Database Connection
Make sure the same database is being used for both create and query operations.

## What the Logs Will Show

After deploying the updated code, Application Insights will show:

1. **Exact UserId values** used for create and query
2. **UserId lengths** to detect whitespace issues
3. **All UserIds in database** if no match is found
4. **Total leave count** to verify data is being saved

This will help identify the exact cause of the mismatch.

---

**Next Step:** Deploy the updated code and check Application Insights logs to see what UserId values are being used.

