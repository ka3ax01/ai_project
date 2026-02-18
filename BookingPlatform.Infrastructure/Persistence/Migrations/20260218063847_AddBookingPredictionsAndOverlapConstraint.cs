using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingPredictionsAndOverlapConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingPredictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Probability = table.Column<double>(type: "double precision", nullable: false),
                    Threshold = table.Column<double>(type: "double precision", nullable: false),
                    PredictedLabel = table.Column<bool>(type: "boolean", nullable: false),
                    ModelVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FeaturesJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingPredictions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingPredictions_BookingId",
                table: "BookingPredictions",
                column: "BookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingPredictions");
        }
    }
}
