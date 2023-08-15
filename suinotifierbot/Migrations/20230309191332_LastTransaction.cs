using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace suinotifierbot.Migrations
{
    /// <inheritdoc />
    public partial class LastTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LastTransaction",
                columns: table => new
                {
                    Digest = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LastTransaction", x => x.Digest);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LastTransaction");
        }
    }
}
