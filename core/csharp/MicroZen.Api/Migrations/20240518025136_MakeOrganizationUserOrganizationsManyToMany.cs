using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroZen.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeOrganizationUserOrganizationsManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationUsers_Organizations_OrganizationId",
                table: "OrganizationUsers");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationUsers_OrganizationId",
                table: "OrganizationUsers");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "OrganizationUsers");

            migrationBuilder.CreateTable(
                name: "OrganizationUserOrganization",
                columns: table => new
                {
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    OrganizationUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationUserOrganization", x => new { x.OrganizationId, x.OrganizationUserId });
                    table.ForeignKey(
                        name: "FK_OrganizationUserOrganization_OrganizationUsers_Organization~",
                        column: x => x.OrganizationUserId,
                        principalTable: "OrganizationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationUserOrganization_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationUserOrganization_OrganizationUserId",
                table: "OrganizationUserOrganization",
                column: "OrganizationUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationUserOrganization");

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "OrganizationUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationUsers_OrganizationId",
                table: "OrganizationUsers",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationUsers_Organizations_OrganizationId",
                table: "OrganizationUsers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
