using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PicStashly.Data.Sqlite.Migrations;

/// <inheritdoc />
public partial class AddCategories : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "CategoryId",
            schema: "app",
            table: "Images",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Categories",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Categories", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Images_CategoryId",
            schema: "app",
            table: "Images",
            column: "CategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_Categories_Name",
            schema: "app",
            table: "Categories",
            column: "Name",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Images_Categories_CategoryId",
            schema: "app",
            table: "Images",
            column: "CategoryId",
            principalSchema: "app",
            principalTable: "Categories",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Images_Categories_CategoryId",
            schema: "app",
            table: "Images");

        migrationBuilder.DropTable(
            name: "Categories",
            schema: "app");

        migrationBuilder.DropIndex(
            name: "IX_Images_CategoryId",
            schema: "app",
            table: "Images");

        migrationBuilder.DropColumn(
            name: "CategoryId",
            schema: "app",
            table: "Images");
    }
}