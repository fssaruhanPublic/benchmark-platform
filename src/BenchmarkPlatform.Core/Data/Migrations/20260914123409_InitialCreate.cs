using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BenchmarkPlatform.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    MachineName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OsDescription = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    RuntimeVersion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProcessorName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ProcessorCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Architecture = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    BuildConfiguration = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    GitCommit = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    GitBranch = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TotalDurationMs = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Namespace = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    TypeName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    MethodName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    Parameters = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    Categories = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    IsBaseline = table.Column<bool>(type: "INTEGER", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    MeanNs = table.Column<double>(type: "REAL", nullable: false),
                    ErrorNs = table.Column<double>(type: "REAL", nullable: false),
                    StdDevNs = table.Column<double>(type: "REAL", nullable: false),
                    MedianNs = table.Column<double>(type: "REAL", nullable: false),
                    MinNs = table.Column<double>(type: "REAL", nullable: false),
                    MaxNs = table.Column<double>(type: "REAL", nullable: false),
                    P95Ns = table.Column<double>(type: "REAL", nullable: false),
                    OpsPerSecond = table.Column<double>(type: "REAL", nullable: false),
                    Gen0 = table.Column<double>(type: "REAL", nullable: false),
                    Gen1 = table.Column<double>(type: "REAL", nullable: false),
                    Gen2 = table.Column<double>(type: "REAL", nullable: false),
                    AllocatedBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    RatioToBaseline = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Results_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Results_FullName_RunId",
                table: "Results",
                columns: new[] { "FullName", "RunId" });

            migrationBuilder.CreateIndex(
                name: "IX_Results_RunId",
                table: "Results",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_Runs_RunAt",
                table: "Runs",
                column: "RunAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Results");

            migrationBuilder.DropTable(
                name: "Runs");
        }
    }
}
