using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestGrid.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddItemsSortIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Items_ModifiedOn_Id",
                table: "Items",
                columns: new[] { "ModifiedOn", "Id" },
                descending: new[] { true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Items_ModifiedOn_Id",
                table: "Items");
        }
    }
}
