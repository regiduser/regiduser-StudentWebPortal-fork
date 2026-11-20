using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentWebPortal.Migrations
{
    /// <inheritdoc />
    public partial class SyncStudentPortalModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Attendances",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now() AT TIME ZONE 'utc'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Attendances",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'utc'",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");
        }
    }
}
