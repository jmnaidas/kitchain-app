using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayWrongCourtCallOut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayRallyEvents_CallOut",
                table: "PlayRallyEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayRallyEvents_CallOut",
                table: "PlayRallyEvents",
                sql: "\"CallOut\" IS NULL OR \"CallOut\" IN ('Drive', 'Dink', 'Lob', 'Fault', 'Out', 'Kitchen', 'ServiceBreak', 'WrongCourt')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayRallyEvents_CallOut",
                table: "PlayRallyEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayRallyEvents_CallOut",
                table: "PlayRallyEvents",
                sql: "\"CallOut\" IS NULL OR \"CallOut\" IN ('Drive', 'Dink', 'Lob', 'Fault', 'Out', 'Kitchen', 'ServiceBreak')");
        }
    }
}
