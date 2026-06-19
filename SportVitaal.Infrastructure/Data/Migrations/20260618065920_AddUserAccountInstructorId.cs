using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportVitaal.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAccountInstructorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InstructorId",
                table: "Users",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InstructorId",
                table: "Users");
        }
    }
}
