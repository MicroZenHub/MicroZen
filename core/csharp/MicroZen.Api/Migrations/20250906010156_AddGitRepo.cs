using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroZen.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGitRepo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GitRepoIconImageUrl",
                table: "Clients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitRepoUrl",
                table: "Clients",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GitRepoIconImageUrl",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "GitRepoUrl",
                table: "Clients");
        }
    }
}
