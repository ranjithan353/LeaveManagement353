-- Fix Id Column Type in LeaveRequests Table
-- Run this in Azure Portal -> SQL Database -> Query Editor
-- 
-- This script fixes the Id column type mismatch issue
-- The Id column should be INT IDENTITY, but it's currently NVARCHAR

-- Step 1: Check current column type
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'LeaveRequests' AND COLUMN_NAME = 'Id';
GO

-- Step 2: Check if there's any data
DECLARE @RowCount INT;
SELECT @RowCount = COUNT(*) FROM [LeaveRequests];
PRINT 'Current row count: ' + CAST(@RowCount AS VARCHAR(10));
GO

-- Step 3: Fix the column type
-- Option A: If table is empty or you can lose data, drop and recreate
IF (SELECT COUNT(*) FROM [LeaveRequests]) = 0
BEGIN
    PRINT 'Table is empty. Dropping and recreating with correct schema...';
    
    IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LeaveRequests]') AND type in (N'U'))
    BEGIN
        DROP TABLE [dbo].[LeaveRequests];
        PRINT 'LeaveRequests table dropped.';
    END
    
    CREATE TABLE [dbo].[LeaveRequests] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        [StartDate] DATETIME2 NOT NULL,
        [EndDate] DATETIME2 NOT NULL,
        [Type] NVARCHAR(50) NOT NULL,
        [Reason] NVARCHAR(1000) NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [AttachmentUrl] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [PK_LeaveRequests] PRIMARY KEY ([Id])
    );
    PRINT 'LeaveRequests table recreated with correct INT IDENTITY column.';
END
ELSE
BEGIN
    PRINT 'Table contains data. You have two options:';
    PRINT '1. Export data, drop table, recreate, import data';
    PRINT '2. Use the application workaround (already implemented in code)';
    PRINT '';
    PRINT 'The application code now handles this automatically with a workaround.';
    PRINT 'To permanently fix, you need to:';
    PRINT '  a) Export all data from LeaveRequests';
    PRINT '  b) Drop the table';
    PRINT '  c) Run the CREATE TABLE statement above';
    PRINT '  d) Re-import the data';
END
GO

-- Step 4: Verify the column type
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'LeaveRequests' AND COLUMN_NAME = 'Id';
GO

PRINT 'Script completed!';

