using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcxiomCRM.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuditLogAppendOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Block UPDATE and DELETE on audit records — inserts still allowed (4.9 append-only)
            migrationBuilder.Sql(@"
CREATE TRIGGER TR_AuditLogs_AppendOnly
ON dbo.AuditLogs
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50001, 'Audit log records are append-only and cannot be modified or deleted.', 1;
END");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_AuditLogs_AppendOnly;");
        }
    }
}
