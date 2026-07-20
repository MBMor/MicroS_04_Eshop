using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrdersService.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenOutboxProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "claimed_at_utc",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "claimed_by",
                table: "outbox_messages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_status_claimed_at_utc",
                table: "outbox_messages",
                columns: new[] { "status", "claimed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_status_published_at_utc",
                table: "outbox_messages",
                columns: new[] { "status", "published_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_status_claimed_at_utc",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_status_published_at_utc",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "claimed_at_utc",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "claimed_by",
                table: "outbox_messages");
        }
    }
}
