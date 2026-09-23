using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Surbibor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSideBetsAndPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Points",
                table: "GameMemberships",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.CreateTable(
                name: "SideBets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PlacingClosesAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpectedResolutionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProposedOutcome = table.Column<bool>(type: "boolean", nullable: true),
                    ProposedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProposedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<bool>(type: "boolean", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SideBets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SideBets_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SideBetWagers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SideBetId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prediction = table.Column<bool>(type: "boolean", nullable: false),
                    Stake = table.Column<int>(type: "integer", nullable: false),
                    PlacedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SideBetWagers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SideBetWagers_SideBets_SideBetId",
                        column: x => x.SideBetId,
                        principalTable: "SideBets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SideBetWagers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SideBets_GameId",
                table: "SideBets",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_SideBetWagers_SideBetId_UserId",
                table: "SideBetWagers",
                columns: new[] { "SideBetId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SideBetWagers_UserId",
                table: "SideBetWagers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SideBetWagers");

            migrationBuilder.DropTable(
                name: "SideBets");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "GameMemberships");
        }
    }
}
