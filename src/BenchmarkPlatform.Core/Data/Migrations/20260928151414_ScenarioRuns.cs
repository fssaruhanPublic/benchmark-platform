using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BenchmarkPlatform.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScenarioRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScenarioRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Suite = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    FinishedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    GitCommit = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    GitBranch = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    MachineName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Target = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    EnvironmentJson = table.Column<string>(type: "TEXT", nullable: false),
                    CaseCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PassedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Group = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScenarioCases_ScenarioRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "ScenarioRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScenarioMetrics_ScenarioCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ScenarioCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScenarioMetrics_ScenarioRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "ScenarioRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioCases_RunId_Name",
                table: "ScenarioCases",
                columns: new[] { "RunId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioMetrics_CaseId",
                table: "ScenarioMetrics",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioMetrics_Name",
                table: "ScenarioMetrics",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioMetrics_RunId_CaseId_Name",
                table: "ScenarioMetrics",
                columns: new[] { "RunId", "CaseId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioRuns_Kind",
                table: "ScenarioRuns",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioRuns_Suite_StartedAt",
                table: "ScenarioRuns",
                columns: new[] { "Suite", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScenarioMetrics");

            migrationBuilder.DropTable(
                name: "ScenarioCases");

            migrationBuilder.DropTable(
                name: "ScenarioRuns");
        }
    }
}
