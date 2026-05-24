IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] int NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE TABLE [SponsorshipTypes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_SponsorshipTypes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [Department] nvarchar(100) NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [RoleId] int NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL DEFAULT (sysutcdatetime()),
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Token] nvarchar(500) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (sysutcdatetime()),
        [RevokedAt] datetime2 NULL,
        [ReplacedByToken] nvarchar(500) NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE TABLE [SponsorshipRequests] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [RequestorId] uniqueidentifier NOT NULL,
        [Department] nvarchar(100) NOT NULL,
        [SponsorshipTypeId] int NOT NULL,
        [EventName] nvarchar(200) NOT NULL,
        [EventDate] date NOT NULL,
        [RequestedAmount] decimal(18,2) NOT NULL,
        [Purpose] nvarchar(2000) NOT NULL,
        [ExpectedBenefit] nvarchar(1000) NULL,
        [Remarks] nvarchar(500) NULL,
        [Status] int NOT NULL,
        [SupportingDocumentPath] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (sysutcdatetime()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_SponsorshipRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SponsorshipRequests_SponsorshipTypes_SponsorshipTypeId] FOREIGN KEY ([SponsorshipTypeId]) REFERENCES [SponsorshipTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SponsorshipRequests_Users_RequestorId] FOREIGN KEY ([RequestorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE TABLE [WorkflowHistory] (
        [Id] bigint NOT NULL IDENTITY,
        [RequestId] uniqueidentifier NOT NULL,
        [ActionById] uniqueidentifier NOT NULL,
        [Action] int NOT NULL,
        [FromStatus] int NOT NULL,
        [ToStatus] int NOT NULL,
        [Remarks] nvarchar(1000) NULL,
        [ActionAt] datetime2 NOT NULL DEFAULT (sysutcdatetime()),
        CONSTRAINT [PK_WorkflowHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkflowHistory_SponsorshipRequests_RequestId] FOREIGN KEY ([RequestId]) REFERENCES [SponsorshipRequests] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_WorkflowHistory_Users_ActionById] FOREIGN KEY ([ActionById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_Token] ON [RefreshTokens] ([Token]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SponsorshipRequests_RequestorId] ON [SponsorshipRequests] ([RequestorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SponsorshipRequests_SponsorshipTypeId] ON [SponsorshipRequests] ([SponsorshipTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SponsorshipRequests_Status] ON [SponsorshipRequests] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SponsorshipTypes_Name] ON [SponsorshipTypes] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_RoleId] ON [Users] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WorkflowHistory_ActionById] ON [WorkflowHistory] ([ActionById]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WorkflowHistory_RequestId] ON [WorkflowHistory] ([RequestId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523200921_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523200921_InitialCreate', N'8.0.11');
END;
GO

COMMIT;
GO

