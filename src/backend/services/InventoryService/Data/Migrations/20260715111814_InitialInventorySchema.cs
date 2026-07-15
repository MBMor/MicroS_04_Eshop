using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryService.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialInventorySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    on_hand_quantity = table.Column<int>(type: "integer", nullable: false),
                    reserved_quantity = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_items", x => x.id);
                    table.CheckConstraint("ck_inventory_items_on_hand_non_negative", "\"on_hand_quantity\" >= 0");
                    table.CheckConstraint("ck_inventory_items_reserved_non_negative", "\"reserved_quantity\" >= 0");
                    table.CheckConstraint("ck_inventory_items_reserved_not_above_on_hand", "\"reserved_quantity\" <= \"on_hand_quantity\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_product_id",
                table: "inventory_items",
                column: "product_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_sku",
                table: "inventory_items",
                column: "sku",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_items");
        }
    }
}
