using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PicStashly.Data.Sql.Migrations;

/// <inheritdoc />
public partial class AddUserIdToEntities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Tags_Name",
            schema: "app",
            table: "Tags");

        migrationBuilder.DropIndex(
            name: "IX_Images_DateAdded",
            schema: "app",
            table: "Images");

        migrationBuilder.DropIndex(
            name: "IX_Categories_Name",
            schema: "app",
            table: "Categories");

        migrationBuilder.AddColumn<string>(
            name: "UserId",
            schema: "app",
            table: "Tags",
            type: "nvarchar(450)",
            maxLength: 450,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "UserId",
            schema: "app",
            table: "Images",
            type: "nvarchar(450)",
            maxLength: 450,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "UserId",
            schema: "app",
            table: "Categories",
            type: "nvarchar(450)",
            maxLength: 450,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_UserId_Name",
            schema: "app",
            table: "Tags",
            columns: new[] { "UserId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Images_UserId_CategoryId",
            schema: "app",
            table: "Images",
            columns: new[] { "UserId", "CategoryId" });

        migrationBuilder.CreateIndex(
            name: "IX_Images_UserId_DateAdded",
            schema: "app",
            table: "Images",
            columns: new[] { "UserId", "DateAdded" });

        migrationBuilder.CreateIndex(
            name: "IX_Categories_UserId_Name",
            schema: "app",
            table: "Categories",
            columns: new[] { "UserId", "Name" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Tags_UserId_Name",
            schema: "app",
            table: "Tags");

        migrationBuilder.DropIndex(
            name: "IX_Images_UserId_CategoryId",
            schema: "app",
            table: "Images");

        migrationBuilder.DropIndex(
            name: "IX_Images_UserId_DateAdded",
            schema: "app",
            table: "Images");

        migrationBuilder.DropIndex(
            name: "IX_Categories_UserId_Name",
            schema: "app",
            table: "Categories");

        migrationBuilder.DropColumn(
            name: "UserId",
            schema: "app",
            table: "Tags");

        migrationBuilder.DropColumn(
            name: "UserId",
            schema: "app",
            table: "Images");

        migrationBuilder.DropColumn(
            name: "UserId",
            schema: "app",
            table: "Categories");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_Name",
            schema: "app",
            table: "Tags",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Images_DateAdded",
            schema: "app",
            table: "Images",
            column: "DateAdded");

        migrationBuilder.CreateIndex(
            name: "IX_Categories_Name",
            schema: "app",
            table: "Categories",
            column: "Name",
            unique: true);
    }
}