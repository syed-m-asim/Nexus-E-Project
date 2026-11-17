using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NexusCommunication.Data;

#nullable disable

namespace NexusCommunication.Migrations
{
    /// <summary>
    /// Adds Customer.CustomerCode (10-digit) with unique index and the UsageRecords table.
    /// Backfills existing empty codes before creating the unique index so this works on any PC
    /// (empty DB or DB that already has customers).
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261021000000_AddCustomerCodeAndUsageRecords")]
    public partial class AddCustomerCodeAndUsageRecords : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Customers') AND name = N'CustomerCode')
BEGIN
    ALTER TABLE [Customers] ADD [CustomerCode] nvarchar(10) NOT NULL
        CONSTRAINT [DF_Customers_CustomerCode] DEFAULT (N'');
END
");

            migrationBuilder.Sql(@"
;WITH ranked AS (
    SELECT [Id], ROW_NUMBER() OVER (ORDER BY [Id]) AS rn
    FROM [Customers]
    WHERE [CustomerCode] IS NULL OR LTRIM(RTRIM([CustomerCode])) = N''
)
UPDATE c
SET [CustomerCode] = RIGHT(REPLICATE(N'0', 10) + CAST(r.rn AS nvarchar(10)), 10)
FROM [Customers] c
INNER JOIN ranked r ON c.[Id] = r.[Id];
");

            migrationBuilder.Sql(@"
;WITH dups AS (
    SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [CustomerCode] ORDER BY [Id]) AS rn
    FROM [Customers]
)
UPDATE c
SET [CustomerCode] = RIGHT(REPLICATE(N'0', 10) + CAST(c.[Id] AS nvarchar(10)), 10)
FROM [Customers] c
INNER JOIN dups d ON c.[Id] = d.[Id]
WHERE d.rn > 1;
");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Customers_CustomerCode' AND object_id = OBJECT_ID(N'dbo.Customers'))
    DROP INDEX [IX_Customers_CustomerCode] ON [Customers];
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Customers_CustomerCode' AND object_id = OBJECT_ID(N'dbo.Customers'))
    CREATE UNIQUE INDEX [IX_Customers_CustomerCode] ON [Customers] ([CustomerCode]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'UsageRecords')
BEGIN
    CREATE TABLE [UsageRecords] (
        [Id] int NOT NULL IDENTITY,
        [ConnectionId] int NOT NULL,
        [HoursUsed] decimal(18,2) NOT NULL,
        [PeriodStart] datetime2 NOT NULL,
        [PeriodEnd] datetime2 NOT NULL,
        [RecordedOn] datetime2 NOT NULL,
        [RecordedByEmployeeId] int NOT NULL,
        [BillId] int NULL,
        [Notes] nvarchar(300) NULL,
        CONSTRAINT [PK_UsageRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UsageRecords_Connections_ConnectionId]
            FOREIGN KEY ([ConnectionId]) REFERENCES [Connections] ([Id]) ON DELETE NO ACTION
    );
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [SequenceCounters] WHERE [Key] = N'Customer')
    INSERT INTO [SequenceCounters] ([Key], [LastValue]) VALUES (N'Customer', 0);

DECLARE @maxCode bigint = 0;
SELECT @maxCode = ISNULL(MAX(TRY_CAST([CustomerCode] AS bigint)), 0) FROM [Customers];
UPDATE [SequenceCounters]
SET [LastValue] = CASE WHEN @maxCode > [LastValue] THEN @maxCode ELSE [LastValue] END
WHERE [Key] = N'Customer';
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'UsageRecords')
    DROP TABLE [UsageRecords];
");
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Customers_CustomerCode' AND object_id = OBJECT_ID(N'dbo.Customers'))
    DROP INDEX [IX_Customers_CustomerCode] ON [Customers];
");
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Customers') AND name = N'CustomerCode')
BEGIN
    DECLARE @df sysname;
    SELECT @df = d.name
    FROM sys.default_constraints d
    JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
    WHERE d.parent_object_id = OBJECT_ID(N'dbo.Customers') AND c.name = N'CustomerCode';
    IF @df IS NOT NULL EXEC(N'ALTER TABLE [Customers] DROP CONSTRAINT [' + @df + N']');
    ALTER TABLE [Customers] DROP COLUMN [CustomerCode];
END
");
        }
    }
}
