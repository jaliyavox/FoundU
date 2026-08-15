using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StudentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    IsSuspended = table.Column<bool>(type: "boolean", nullable: false),
                    SuspensionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    SuspendedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUsers_AppUsers_SuspendedByUserId",
                        column: x => x.SuspendedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampusLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Building = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampusLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorageLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Building = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemTypes_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FoundReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoundLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneralDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PrivateVerificationDetails = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ObservedAttributesJson = table.Column<string>(type: "jsonb", nullable: true),
                    PrivateVerificationAttributesJson = table.Column<string>(type: "jsonb", nullable: true),
                    PrimaryColor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SecondaryColor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FoundAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoundReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoundReports_AppUsers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoundReports_CampusLocations_FoundLocationId",
                        column: x => x.FoundLocationId,
                        principalTable: "CampusLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoundReports_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoundReports_ItemTypes_ItemTypeId",
                        column: x => x.ItemTypeId,
                        principalTable: "ItemTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoundReports_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LostReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastSeenLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PrimaryColor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SecondaryColor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IdentifyingFeaturesJson = table.Column<string>(type: "jsonb", nullable: true),
                    ParsedAttributesJson = table.Column<string>(type: "jsonb", nullable: true),
                    EstimatedLostFromAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    EstimatedLostToAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WithdrawReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostReports", x => x.Id);
                    table.CheckConstraint("CK_LostReports_EstimatedLostRange", "\"EstimatedLostFromAt\" <= \"EstimatedLostToAt\"");
                    table.ForeignKey(
                        name: "FK_LostReports_AppUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostReports_CampusLocations_LastSeenLocationId",
                        column: x => x.LastSeenLocationId,
                        principalTable: "CampusLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostReports_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostReports_ItemTypes_ItemTypeId",
                        column: x => x.ItemTypeId,
                        principalTable: "ItemTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FoundItemPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FoundReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoundItemPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoundItemPhotos_FoundReports_FoundReportId",
                        column: x => x.FoundReportId,
                        principalTable: "FoundReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FoundReportStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FoundReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoundReportStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoundReportStatusHistories_AppUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FoundReportStatusHistories_FoundReports_FoundReportId",
                        column: x => x.FoundReportId,
                        principalTable: "FoundReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StorageTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FoundReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStorageLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToStorageLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferredByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TransferredAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageTransfers_AppUsers_TransferredByUserId",
                        column: x => x.TransferredByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageTransfers_FoundReports_FoundReportId",
                        column: x => x.FoundReportId,
                        principalTable: "FoundReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StorageTransfers_StorageLocations_FromStorageLocationId",
                        column: x => x.FromStorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageTransfers_StorageLocations_ToStorageLocationId",
                        column: x => x.ToStorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LostReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoundReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Claims_AppUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Claims_FoundReports_FoundReportId",
                        column: x => x.FoundReportId,
                        principalTable: "FoundReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Claims_LostReports_LostReportId",
                        column: x => x.LostReportId,
                        principalTable: "LostReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LostItemPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LostReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostItemPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostItemPhotos_LostReports_LostReportId",
                        column: x => x.LostReportId,
                        principalTable: "LostReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LostReportStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LostReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostReportStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostReportStatusHistories_AppUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LostReportStatusHistories_LostReports_LostReportId",
                        column: x => x.LostReportId,
                        principalTable: "LostReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    TriggerEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TriggerEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Objective = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PlanJson = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FinalOutcomeJson = table.Column<string>(type: "jsonb", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentRuns_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsOverride = table.Column<bool>(type: "boolean", nullable: false),
                    OverriddenByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalDecisions_AppUsers_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalDecisions_AppUsers_OverriddenByUserId",
                        column: x => x.OverriddenByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalDecisions_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClaimStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimStatusHistories_AppUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClaimStatusHistories_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    Task = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    InputJson = table.Column<string>(type: "jsonb", nullable: true),
                    OutputJson = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentSteps_AgentRuns_AgentRunId",
                        column: x => x.AgentRunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LostReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoundReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchScore = table.Column<decimal>(type: "numeric(4,3)", nullable: false),
                    MatchingFactorsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ConflictingFactorsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GeneratedByAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSuggestions", x => x.Id);
                    table.CheckConstraint("CK_MatchSuggestions_MatchScore_Range", "\"MatchScore\" >= 0 AND \"MatchScore\" <= 1");
                    table.ForeignKey(
                        name: "FK_MatchSuggestions_AgentRuns_GeneratedByAgentRunId",
                        column: x => x.GeneratedByAgentRunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MatchSuggestions_FoundReports_FoundReportId",
                        column: x => x.FoundReportId,
                        principalTable: "FoundReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchSuggestions_LostReports_LostReportId",
                        column: x => x.LostReportId,
                        principalTable: "LostReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VerificationQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    GeneratedByAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerificationQuestions_AgentRuns_GeneratedByAgentRunId",
                        column: x => x.GeneratedByAgentRunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VerificationQuestions_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchSuggestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchStatusHistories_AppUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MatchStatusHistories_MatchSuggestions_MatchSuggestionId",
                        column: x => x.MatchSuggestionId,
                        principalTable: "MatchSuggestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClaimAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnswerText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimAnswers_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClaimAnswers_VerificationQuestions_VerificationQuestionId",
                        column: x => x.VerificationQuestionId,
                        principalTable: "VerificationQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "CampusLocations",
                columns: new[] { "Id", "Building", "CreatedAt", "DeletedAt", "Description", "IsActive", "IsDeleted", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("31111111-1111-1111-1111-111111111111"), "Building C", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, false, "Library", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("31111111-1111-1111-1111-111111111112"), "Building B", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, false, "Lecture Hall B12", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("31111111-1111-1111-1111-111111111113"), "Building A", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, false, "Cafeteria", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("31111111-1111-1111-1111-111111111114"), "Sports Block", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, false, "Sports Complex", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("31111111-1111-1111-1111-111111111115"), "Building A", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, false, "Main Auditorium", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("31111111-1111-1111-1111-111111111116"), "Building A", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, false, "Security Desk", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("31111111-1111-1111-1111-111111111117"), "Outdoor", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, false, "Parking Lot", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "Description", "IsActive", "IsDeleted", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("21111111-1111-1111-1111-111111111111"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Phones, laptops, chargers, headphones", true, false, "Electronics", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("21111111-1111-1111-1111-111111111112"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Backpacks, handbags, wallets, purses", true, false, "Bags & Wallets", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("21111111-1111-1111-1111-111111111113"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Jackets, hats, scarves and other apparel", true, false, "Clothing", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("21111111-1111-1111-1111-111111111114"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Student IDs, cards, documents", true, false, "Documents & Cards", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("21111111-1111-1111-1111-111111111115"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "House, locker, or vehicle keys", true, false, "Keys", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("21111111-1111-1111-1111-111111111116"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Watches, jewelry, glasses", true, false, "Jewelry & Accessories", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("21111111-1111-1111-1111-111111111117"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Textbooks, notebooks, stationery", true, false, "Books & Stationery", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("21111111-1111-1111-1111-111111111118"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Anything not covered above", true, false, "Other", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "StorageLocations",
                columns: new[] { "Id", "Building", "Capacity", "CreatedAt", "DeletedAt", "IsActive", "IsDeleted", "Name", "UpdatedAt" },
                values: new object[] { new Guid("41111111-1111-1111-1111-111111111111"), "Building A", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Security Desk - Building A", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "ItemTypes",
                columns: new[] { "Id", "CategoryId", "CreatedAt", "DeletedAt", "IsActive", "IsDeleted", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("51111111-1111-1111-1111-111111111111"), new Guid("21111111-1111-1111-1111-111111111111"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Laptop", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111112"), new Guid("21111111-1111-1111-1111-111111111111"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Phone", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111113"), new Guid("21111111-1111-1111-1111-111111111111"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Headphones", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111114"), new Guid("21111111-1111-1111-1111-111111111111"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Earphones", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111115"), new Guid("21111111-1111-1111-1111-111111111112"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Backpack", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111116"), new Guid("21111111-1111-1111-1111-111111111112"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Laptop Bag", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111117"), new Guid("21111111-1111-1111-1111-111111111112"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Purse", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111118"), new Guid("21111111-1111-1111-1111-111111111112"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Wallet", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_ClaimId",
                table: "AgentRuns",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_Status",
                table: "AgentRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_TriggerEntityType_TriggerEntityId",
                table: "AgentRuns",
                columns: new[] { "TriggerEntityType", "TriggerEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentSteps_AgentRunId_StepOrder",
                table: "AgentSteps",
                columns: new[] { "AgentRunId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_ClaimId",
                table: "ApprovalDecisions",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_DecidedByUserId",
                table: "ApprovalDecisions",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_IsOverride",
                table: "ApprovalDecisions",
                column: "IsOverride");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_OverriddenByUserId",
                table: "ApprovalDecisions",
                column: "OverriddenByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_IsSuspended",
                table: "AppUsers",
                column: "IsSuspended");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_NormalizedEmail",
                table: "AppUsers",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Role",
                table: "AppUsers",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_StudentNumber_Unique",
                table: "AppUsers",
                column: "StudentNumber",
                unique: true,
                filter: "\"StudentNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_SuspendedByUserId",
                table: "AppUsers",
                column: "SuspendedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CampusLocations_Name_Building",
                table: "CampusLocations",
                columns: new[] { "Name", "Building" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAnswers_ClaimId",
                table: "ClaimAnswers",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAnswers_VerificationQuestionId",
                table: "ClaimAnswers",
                column: "VerificationQuestionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_FoundReportId_OneApproved",
                table: "Claims",
                column: "FoundReportId",
                unique: true,
                filter: "\"Status\" = 'Approved'");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_LostReportId",
                table: "Claims",
                column: "LostReportId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Status",
                table: "Claims",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_StudentId",
                table: "Claims",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimStatusHistories_ChangedAt",
                table: "ClaimStatusHistories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimStatusHistories_ChangedByUserId",
                table: "ClaimStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimStatusHistories_ClaimId",
                table: "ClaimStatusHistories",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundItemPhotos_FoundReportId",
                table: "FoundItemPhotos",
                column: "FoundReportId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReports_CategoryId",
                table: "FoundReports",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReports_FoundAt",
                table: "FoundReports",
                column: "FoundAt");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReports_FoundLocationId",
                table: "FoundReports",
                column: "FoundLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReports_ItemTypeId",
                table: "FoundReports",
                column: "ItemTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReports_StaffId",
                table: "FoundReports",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReports_Status",
                table: "FoundReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReports_Status_CategoryId_ItemTypeId_FoundLocationId",
                table: "FoundReports",
                columns: new[] { "Status", "CategoryId", "ItemTypeId", "FoundLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_FoundReports_StorageLocationId",
                table: "FoundReports",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReportStatusHistories_ChangedAt",
                table: "FoundReportStatusHistories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReportStatusHistories_ChangedByUserId",
                table: "FoundReportStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReportStatusHistories_FoundReportId",
                table: "FoundReportStatusHistories",
                column: "FoundReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTypes_CategoryId_Name",
                table: "ItemTypes",
                columns: new[] { "CategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LostItemPhotos_LostReportId",
                table: "LostItemPhotos",
                column: "LostReportId");

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_CategoryId",
                table: "LostReports",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_EstimatedLostFromAt_EstimatedLostToAt",
                table: "LostReports",
                columns: new[] { "EstimatedLostFromAt", "EstimatedLostToAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_ItemTypeId",
                table: "LostReports",
                column: "ItemTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_LastSeenLocationId",
                table: "LostReports",
                column: "LastSeenLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_Status",
                table: "LostReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_Status_CategoryId_ItemTypeId_LastSeenLocationId",
                table: "LostReports",
                columns: new[] { "Status", "CategoryId", "ItemTypeId", "LastSeenLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_StudentId",
                table: "LostReports",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_LostReportStatusHistories_ChangedAt",
                table: "LostReportStatusHistories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LostReportStatusHistories_ChangedByUserId",
                table: "LostReportStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LostReportStatusHistories_LostReportId",
                table: "LostReportStatusHistories",
                column: "LostReportId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchStatusHistories_ChangedAt",
                table: "MatchStatusHistories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MatchStatusHistories_ChangedByUserId",
                table: "MatchStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchStatusHistories_MatchSuggestionId",
                table: "MatchStatusHistories",
                column: "MatchSuggestionId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchSuggestions_FoundReportId",
                table: "MatchSuggestions",
                column: "FoundReportId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchSuggestions_GeneratedByAgentRunId",
                table: "MatchSuggestions",
                column: "GeneratedByAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchSuggestions_LostReportId_FoundReportId",
                table: "MatchSuggestions",
                columns: new[] { "LostReportId", "FoundReportId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchSuggestions_MatchScore",
                table: "MatchSuggestions",
                column: "MatchScore");

            migrationBuilder.CreateIndex(
                name: "IX_MatchSuggestions_Status",
                table: "MatchSuggestions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedAt",
                table: "Notifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_StorageLocations_Name",
                table: "StorageLocations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransfers_FoundReportId",
                table: "StorageTransfers",
                column: "FoundReportId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransfers_FromStorageLocationId",
                table: "StorageTransfers",
                column: "FromStorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransfers_ToStorageLocationId",
                table: "StorageTransfers",
                column: "ToStorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransfers_TransferredAt",
                table: "StorageTransfers",
                column: "TransferredAt");

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransfers_TransferredByUserId",
                table: "StorageTransfers",
                column: "TransferredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationQuestions_ClaimId",
                table: "VerificationQuestions",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationQuestions_GeneratedByAgentRunId",
                table: "VerificationQuestions",
                column: "GeneratedByAgentRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentSteps");

            migrationBuilder.DropTable(
                name: "ApprovalDecisions");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ClaimAnswers");

            migrationBuilder.DropTable(
                name: "ClaimStatusHistories");

            migrationBuilder.DropTable(
                name: "FoundItemPhotos");

            migrationBuilder.DropTable(
                name: "FoundReportStatusHistories");

            migrationBuilder.DropTable(
                name: "LostItemPhotos");

            migrationBuilder.DropTable(
                name: "LostReportStatusHistories");

            migrationBuilder.DropTable(
                name: "MatchStatusHistories");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "StorageTransfers");

            migrationBuilder.DropTable(
                name: "VerificationQuestions");

            migrationBuilder.DropTable(
                name: "MatchSuggestions");

            migrationBuilder.DropTable(
                name: "AgentRuns");

            migrationBuilder.DropTable(
                name: "Claims");

            migrationBuilder.DropTable(
                name: "FoundReports");

            migrationBuilder.DropTable(
                name: "LostReports");

            migrationBuilder.DropTable(
                name: "StorageLocations");

            migrationBuilder.DropTable(
                name: "AppUsers");

            migrationBuilder.DropTable(
                name: "CampusLocations");

            migrationBuilder.DropTable(
                name: "ItemTypes");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
