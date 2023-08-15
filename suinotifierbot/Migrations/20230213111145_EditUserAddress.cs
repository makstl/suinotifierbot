using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace suinotifierbot.Migrations
{
    /// <inheritdoc />
    public partial class EditUserAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EditUserAddressId",
                table: "User",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EditUserAddressId",
                table: "User");
        }
    }
}
