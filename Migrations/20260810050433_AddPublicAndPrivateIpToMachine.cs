using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfoClusMonitor.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicAndPrivateIpToMachine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrivateIpAddress",
                table: "Machines",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PublicIpAddress",
                table: "Machines",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrivateIpAddress",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "PublicIpAddress",
                table: "Machines");
        }
    }
}
