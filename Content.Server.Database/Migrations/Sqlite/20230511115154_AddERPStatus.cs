using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddERPStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropTable(
            //    name: "sponsors");

            migrationBuilder.AddColumn<int>(
                name: "erpstatus",
                table: "profile",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "erpstatus",
                table: "profile");

            //migrationBuilder.CreateTable(
            //    name: "sponsors",
            //    columns: table => new
            //    {
            //        user_id = table.Column<Guid>(type: "TEXT", nullable: false),
            //        allow_job = table.Column<bool>(type: "INTEGER", nullable: false),
            //        allowed_markings = table.Column<string>(type: "TEXT", nullable: false),
            //        expire_date = table.Column<DateTime>(type: "TEXT", nullable: false),
            //        extra_slots = table.Column<int>(type: "INTEGER", nullable: false),
            //        have_priority_join = table.Column<bool>(type: "INTEGER", nullable: false),
            //        ooccolor = table.Column<string>(type: "TEXT", nullable: false),
            //        tier = table.Column<int>(type: "INTEGER", nullable: false)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_sponsors", x => x.user_id);
            //    });

            //migrationBuilder.CreateIndex(
            //    name: "IX_sponsors_user_id",
            //    table: "sponsors",
            //    column: "user_id",
            //    unique: true);
        }
    }
}
