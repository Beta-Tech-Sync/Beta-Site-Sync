using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SiteSyncTracker.Data;

namespace SiteSyncTracker.Web.Migrations.Inventory;

[DbContext(typeof(AppDbContext))]
[Migration("20261114000000_ClientWorkspaces")]
public sealed class ClientWorkspaces : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateTable(
        name:"Workspaces",
        columns:table=>new {
            OwnerId=table.Column<string>(nullable:false),
            Revision=table.Column<int>(nullable:false),
            DataJson=table.Column<string>(nullable:false)
        },
        constraints:table=>table.PrimaryKey("PK_Workspaces",x=>x.OwnerId));
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("Workspaces");
}
