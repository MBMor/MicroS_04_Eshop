using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryStockAdjustmentOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_stock_adjustment_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_delta = table.Column<int>(type: "integer", nullable: false),
                    expected_version = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    actor_subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trace_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    on_hand_before = table.Column<int>(type: "integer", nullable: true),
                    reserved_before = table.Column<int>(type: "integer", nullable: true),
                    available_before = table.Column<int>(type: "integer", nullable: true),
                    on_hand_after = table.Column<int>(type: "integer", nullable: true),
                    reserved_after = table.Column<int>(type: "integer", nullable: true),
                    available_after = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true),
                    item_created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    item_updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result_version = table.Column<long>(type: "bigint", nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_stock_adjustment_operations", x => x.id);
                    table.CheckConstraint("ck_inventory_stock_adjustments_expected_version_positive", "\"expected_version\" > 0");
                    table.CheckConstraint("ck_inventory_stock_adjustments_quantity_delta_non_zero", "\"quantity_delta\" <> 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_adjustment_operations_idempotency_key",
                table: "inventory_stock_adjustment_operations",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_adjustment_operations_inventory_item_id_occ~",
                table: "inventory_stock_adjustment_operations",
                columns: new[] { "inventory_item_id", "occurred_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_stock_adjustment_operations");
        }
    }
}
