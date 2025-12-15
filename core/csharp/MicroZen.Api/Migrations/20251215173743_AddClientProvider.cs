using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroZen.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddClientProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Provider",
                table: "OAuth2ClientConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Provider",
                table: "OAuth2ClientConfigs");
        }
    }
}
