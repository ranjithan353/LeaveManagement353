using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaveManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProfileAvatar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "EmployeeProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarFileName",
                table: "EmployeeProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "EmployeeProfiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "AvatarFileName",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "EmployeeProfiles");
        }
    }
}
