using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PicStashly.Data.MySql.Migrations;

/// <inheritdoc />
public partial class AddUserIdToEntities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Tags_Name",
            table: "Tags");

        migrationBuilder.DropIndex(
            name: "IX_Images_DateAdded",
            table: "Images");

        migrationBuilder.DropIndex(
            name: "IX_Categories_Name",
            table: "Categories");

        migrationBuilder.AddColumn<string>(
            name: "UserId",
            table: "Tags",
            type: "varchar(450)",
            maxLength: 450,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "UserId",
            table: "Images",
            type: "varchar(450)",
            maxLength: 450,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "UserId",
            table: "Categories",
            type: "varchar(450)",
            maxLength: 450,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_UserId_Name",
            table: "Tags",
            columns: new[] { "UserId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Images_UserId_CategoryId",
            table: "Images",
            columns: new[] { "UserId", "CategoryId" });

        migrationBuilder.CreateIndex(
            name: "IX_Images_UserId_DateAdded",
            table: "Images",
            columns: new[] { "UserId", "DateAdded" });

        migrationBuilder.CreateIndex(
            name: "IX_Categories_UserId_Name",
            table: "Categories",
            columns: new[] { "UserId", "Name" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Tags_UserId_Name",
            table: "Tags");

        migrationBuilder.DropIndex(
            name: "IX_Images_UserId_CategoryId",
            table: "Images");

        migrationBuilder.DropIndex(
            name: "IX_Images_UserId_DateAdded",
            table: "Images");

        migrationBuilder.DropIndex(
            name: "IX_Categories_UserId_Name",
            table: "Categories");

        migrationBuilder.DropColumn(
            name: "UserId",
            table: "Tags");

        migrationBuilder.DropColumn(
            name: "UserId",
            table: "Images");

        migrationBuilder.DropColumn(
            name: "UserId",
            table: "Categories");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_Name",
            table: "Tags",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Images_DateAdded",
            table: "Images",
            column: "DateAdded");

        migrationBuilder.CreateIndex(
            name: "IX_Categories_Name",
            table: "Categories",
            column: "Name",
            unique: true);
    }
}