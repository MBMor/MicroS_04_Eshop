using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrdersService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderStatusHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    to_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_status_history_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                    INSERT INTO order_status_history
                    (
                        id,
                        order_id,
                        from_status,
                        to_status,
                        reason,
                        changed_at_utc
                    )
                    SELECT
                        gen_random_uuid(),
                        id,
                        NULL,
                        status,
                        'Current order state imported when status history was introduced.',
                        COALESCE(updated_at_utc, created_at_utc)
                    FROM orders;
                    """);

            migrationBuilder.CreateIndex(
                name: "IX_order_status_history_order_id_changed_at_utc",
                table: "order_status_history",
                columns: new[] { "order_id", "changed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_status_history");
        }
    }
}
