# Leave Management (Local)

This is a minimal Leave Management Razor Pages app (.NET 8) for local development.

Features (local):
- Mock authentication (cookie-based) with two users: `user/123` (Employee) and `manager/123` (Manager).
- Employees create leave requests with optional attachment (saved to local Data/Uploads and served at /uploads).
- Managers view pending requests and Approve/Reject.
- SQLite local DB (Data/leave.db) used by default.

Prereqs
- .NET 8 SDK
- PowerShell (on Windows)

Quick run (from repository root `c:\Leavemanagement1`):

1) Restore and build

```powershell
cd LeaveManagement
dotnet restore
dotnet build
```

2) Run the app

```powershell
dotnet run --project .\LeaveManagement.csproj
```

3) Open a browser: http://localhost:5000 (or the HTTPS/HTTP URL printed by dotnet run)

Login (local):
- Go to /Account/Login and pick `user` or `manager`.

Create a leave:
- After login as `user`, go to `Create` and fill Start/End/Type/Reason and optionally upload a file (pdf/jpg/docx). Files are stored under `Data/Uploads` and served at `/uploads`.

Manager approval:
- Login as `manager` and go to `Manager` to view pending requests and Approve/Reject.

Run tests

```powershell
cd ..\LeaveManagement.Tests
dotnet test
```

Notes
- This local setup uses cookie auth and local file store. When deploying to Azure, swap LocalFileStore with AzureBlobFileStore and configure Azure AD authentication.
