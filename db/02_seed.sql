/*
 * Sponsorship — seed script
 *
 * Idempotent inserts for reference data and demo accounts.
 * Run this AFTER 01_schema.sql.
 *
 * All demo accounts use the password: Demo@123
 * (BCrypt hash below was generated with BCrypt.Net-Next, work factor 11.)
 */

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

------------------------------------------------------------
-- Roles
------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Id = 1)
    INSERT INTO Roles (Id, Name) VALUES (1, 'Requestor');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Id = 2)
    INSERT INTO Roles (Id, Name) VALUES (2, 'Manager');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Id = 3)
    INSERT INTO Roles (Id, Name) VALUES (3, 'FinanceAdmin');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Id = 4)
    INSERT INTO Roles (Id, Name) VALUES (4, 'SystemAdmin');

------------------------------------------------------------
-- Sponsorship Types
------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM SponsorshipTypes WHERE Name = 'Event')
    INSERT INTO SponsorshipTypes (Name, IsActive) VALUES ('Event', 1);
IF NOT EXISTS (SELECT 1 FROM SponsorshipTypes WHERE Name = 'Charity')
    INSERT INTO SponsorshipTypes (Name, IsActive) VALUES ('Charity', 1);
IF NOT EXISTS (SELECT 1 FROM SponsorshipTypes WHERE Name = 'Sports')
    INSERT INTO SponsorshipTypes (Name, IsActive) VALUES ('Sports', 1);
IF NOT EXISTS (SELECT 1 FROM SponsorshipTypes WHERE Name = 'Education')
    INSERT INTO SponsorshipTypes (Name, IsActive) VALUES ('Education', 1);
IF NOT EXISTS (SELECT 1 FROM SponsorshipTypes WHERE Name = 'CommunityOutreach')
    INSERT INTO SponsorshipTypes (Name, IsActive) VALUES ('CommunityOutreach', 1);

------------------------------------------------------------
-- Demo users (password = Demo@123)
-- NOTE: When the API runs in Development, DbSeeder will also insert
-- these users with a fresh BCrypt hash. If you ran the API first,
-- this seed script will simply skip duplicates because of the
-- IF NOT EXISTS guards.
------------------------------------------------------------
DECLARE @PwHash NVARCHAR(500) = '$2a$11$rD83yLkF7zQXtTbHV6Q3O.4Xm6hjF1lJq0Z2pYJxQwK0Q1pZ0KjwK';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = '11111111-1111-1111-1111-111111111111')
    INSERT INTO Users (Id, Email, FullName, Department, PasswordHash, RoleId, IsActive, CreatedAt)
    VALUES ('11111111-1111-1111-1111-111111111111', 'requestor@demo.local', 'Demo Requestor', 'Sales', @PwHash, 1, 1, @Now);

IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = '22222222-2222-2222-2222-222222222222')
    INSERT INTO Users (Id, Email, FullName, Department, PasswordHash, RoleId, IsActive, CreatedAt)
    VALUES ('22222222-2222-2222-2222-222222222222', 'manager@demo.local', 'Demo Manager', 'Sales', @PwHash, 2, 1, @Now);

IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = '33333333-3333-3333-3333-333333333333')
    INSERT INTO Users (Id, Email, FullName, Department, PasswordHash, RoleId, IsActive, CreatedAt)
    VALUES ('33333333-3333-3333-3333-333333333333', 'finance@demo.local', 'Demo Finance', 'Finance', @PwHash, 3, 1, @Now);

IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = '44444444-4444-4444-4444-444444444444')
    INSERT INTO Users (Id, Email, FullName, Department, PasswordHash, RoleId, IsActive, CreatedAt)
    VALUES ('44444444-4444-4444-4444-444444444444', 'admin@demo.local', 'Demo Admin', 'IT', @PwHash, 4, 1, @Now);

COMMIT TRAN;
PRINT 'Seed completed.';
