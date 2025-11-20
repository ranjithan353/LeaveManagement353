-- SQL Server Script to Create Tables with IDENTITY Columns
-- Run this in Azure Portal -> SQL Database -> Query Editor

-- Create LeaveRequests table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LeaveRequests]') AND type in (N'U'))
BEGIN
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
    PRINT 'LeaveRequests table created successfully.';
END
ELSE
BEGIN
    PRINT 'LeaveRequests table already exists.';
END
GO

-- Create EmployeeProfiles table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EmployeeProfiles]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[EmployeeProfiles] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        [FullName] NVARCHAR(200) NOT NULL,
        [Email] NVARCHAR(MAX) NULL,
        [Department] NVARCHAR(MAX) NULL,
        [Address] NVARCHAR(MAX) NULL,
        [Phone] NVARCHAR(MAX) NULL,
        [Role] NVARCHAR(MAX) NULL,
        [AvatarFileName] NVARCHAR(MAX) NULL,
        [HireDate] DATETIME2 NULL,
        CONSTRAINT [PK_EmployeeProfiles] PRIMARY KEY ([Id])
    );
    PRINT 'EmployeeProfiles table created successfully.';
END
ELSE
BEGIN
    PRINT 'EmployeeProfiles table already exists.';
END
GO

-- Create __EFMigrationsHistory table if it doesn't exist (for EF Core migrations tracking)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[__EFMigrationsHistory]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory] (
        [MigrationId] NVARCHAR(150) NOT NULL,
        [ProductVersion] NVARCHAR(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
    PRINT '__EFMigrationsHistory table created successfully.';
END
GO

PRINT 'All tables created/verified successfully!';

