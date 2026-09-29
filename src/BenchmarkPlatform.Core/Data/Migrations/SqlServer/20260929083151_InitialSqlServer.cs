using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BenchmarkPlatform.Core.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunAt = table.Column<long>(type: "bigint", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MachineName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OsDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RuntimeVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProcessorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ProcessorCount = table.Column<int>(type: "int", nullable: false),
                    Architecture = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BuildConfiguration = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GitCommit = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    GitBranch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TotalDurationMs = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Suite = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartedAt = table.Column<long>(type: "bigint", nullable: false),
                    FinishedAt = table.Column<long>(type: "bigint", nullable: false),
                    GitCommit = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    GitBranch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MachineName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Target = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EnvironmentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CaseCount = table.Column<int>(type: "int", nullable: false),
                    PassedCount = table.Column<int>(type: "int", nullable: false),
                    ImportedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Namespace = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TypeName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MethodName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Parameters = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Categories = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsBaseline = table.Column<bool>(type: "bit", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    MeanNs = table.Column<double>(type: "float", nullable: false),
                    ErrorNs = table.Column<double>(type: "float", nullable: false),
                    StdDevNs = table.Column<double>(type: "float", nullable: false),
                    MedianNs = table.Column<double>(type: "float", nullable: false),
                    MinNs = table.Column<double>(type: "float", nullable: false),
                    MaxNs = table.Column<double>(type: "float", nullable: false),
                    P95Ns = table.Column<double>(type: "float", nullable: false),
                    OpsPerSecond = table.Column<double>(type: "float", nullable: false),
                    Gen0 = table.Column<double>(type: "float", nullable: false),
                    Gen1 = table.Column<double>(type: "float", nullable: false),
                    Gen2 = table.Column<double>(type: "float", nullable: false),
                    AllocatedBytes = table.Column<long>(type: "bigint", nullable: false),
                    RatioToBaseline = table.Column<double>(type: "float", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "ScenarioCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Group = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Value = table.Column<double>(type: "float", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScenarioMetrics_ScenarioCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ScenarioCases",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ScenarioMetrics_ScenarioRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "ScenarioRuns",
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
                name: "Results");

            migrationBuilder.DropTable(
                name: "ScenarioMetrics");

            migrationBuilder.DropTable(
                name: "Runs");

            migrationBuilder.DropTable(
                name: "ScenarioCases");

            migrationBuilder.DropTable(
                name: "ScenarioRuns");
        }
    }
}
