using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace suinotifierbot.Migrations
{
    /// <inheritdoc />
    public partial class IsDelegate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDelegate",
                table: "UserAddress",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDelegate",
                table: "UserAddress");
        }
    }
}
