using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdditionalArchiveContents",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    SourcePath = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: true
                    ),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TextContent = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdditionalArchiveContents", x => x.Id);
                    table.CheckConstraint(
                        "CK_AdditionalArchiveContent_FieldsMatchType",
                        "(\"Type\" = 1 AND \"SourcePath\" IS NOT NULL AND \"FileName\" IS NULL AND \"TextContent\" IS NULL) OR (\"Type\" = 2 AND \"SourcePath\" IS NULL AND \"FileName\" IS NOT NULL AND \"TextContent\" IS NOT NULL)"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ApplicationConfigurationOverrides",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConfigurationKey = table.Column<string>(
                        type: "TEXT",
                        maxLength: 200,
                        nullable: false
                    ),
                    PropertyName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 200,
                        nullable: false
                    ),
                    SerializedValue = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationConfigurationOverrides", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "BackgroundTaskStates",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DisplayName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 200,
                        nullable: false
                    ),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultInterval = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    IntervalOverride = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    LastStartedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                    LastFinishedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                    LastExecutionStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    LastErrorMessage = table.Column<string>(
                        type: "TEXT",
                        maxLength: 2000,
                        nullable: true
                    ),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundTaskStates", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "DistributionSiteRegistrations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DistributionSiteClassName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: false
                    ),
                    SerializedConfig = table.Column<string>(
                        type: "TEXT",
                        maxLength: 4000,
                        nullable: false
                    ),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableAutomaticPosting = table.Column<bool>(type: "INTEGER", nullable: false),
                    StripDotsForThreadSearch = table.Column<bool>(type: "INTEGER", nullable: false),
                    EncryptedSession = table.Column<string>(
                        type: "TEXT",
                        maxLength: 8000,
                        nullable: true
                    ),
                    HasUnreadableSecrets = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionSiteRegistrations", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "ForumPostTemplates",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    TemplateBody = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumPostTemplates", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "MediaDatabaseRegistrations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaDatabaseClassName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: false
                    ),
                    SerializedConfig = table.Column<string>(
                        type: "TEXT",
                        maxLength: 4000,
                        nullable: false
                    ),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasUnreadableSecrets = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaDatabaseRegistrations", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "NfoDatabaseRegistrations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NfoDatabaseClassName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: false
                    ),
                    SerializedConfig = table.Column<string>(
                        type: "TEXT",
                        maxLength: 4000,
                        nullable: false
                    ),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasUnreadableSecrets = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NfoDatabaseRegistrations", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "ProxyServers",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProxyType = table.Column<int>(type: "INTEGER", nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    EncryptedPassword = table.Column<string>(type: "text", nullable: true),
                    HasUnreadableSecrets = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProxyServers", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "QualityProfiles",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityProfiles", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "ReleaseFolderObservations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FolderPath = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: false
                    ),
                    FileCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    LastChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseFolderObservations", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "RemoteSourceRegistrations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SerializedConfig = table.Column<string>(type: "text", nullable: false),
                    SourceClassName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: false
                    ),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasUnreadableSecrets = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxConnections = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteSourceRegistrations", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "TelegramConfigurations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EncryptedBotToken = table.Column<string>(type: "TEXT", nullable: false),
                    HasUnreadableSecrets = table.Column<bool>(type: "INTEGER", nullable: false),
                    BotUsername = table.Column<string>(
                        type: "TEXT",
                        maxLength: 64,
                        nullable: false
                    ),
                    NotificationBaseUrl = table.Column<string>(
                        type: "TEXT",
                        maxLength: 2000,
                        nullable: false
                    ),
                    ChatId = table.Column<long>(type: "INTEGER", nullable: true),
                    ChatName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ForwardInfo = table.Column<bool>(type: "INTEGER", nullable: false),
                    ForwardWarning = table.Column<bool>(type: "INTEGER", nullable: false),
                    ForwardError = table.Column<bool>(type: "INTEGER", nullable: false),
                    PairingTokenHash = table.Column<string>(
                        type: "TEXT",
                        maxLength: 64,
                        nullable: true
                    ),
                    PairingExpiresAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                    UpdateOffset = table.Column<long>(type: "INTEGER", nullable: false),
                    ForwardNotificationsAfterId = table.Column<int>(
                        type: "INTEGER",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramConfigurations", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "ForumPostingRules",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DistributionSiteRegistrationId = table.Column<int>(
                        type: "INTEGER",
                        nullable: false
                    ),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ConditionJson = table.Column<string>(
                        type: "TEXT",
                        maxLength: 8000,
                        nullable: false
                    ),
                    TargetNodeId = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: false
                    ),
                    TargetPathSnapshot = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: false
                    ),
                    ThreadPrefixId = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: true
                    ),
                    ForumPostTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    PostMode = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumPostingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForumPostingRules_DistributionSiteRegistrations_DistributionSiteRegistrationId",
                        column: x => x.DistributionSiteRegistrationId,
                        principalTable: "DistributionSiteRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ForumPostingRules_ForumPostTemplates_ForumPostTemplateId",
                        column: x => x.ForumPostTemplateId,
                        principalTable: "ForumPostTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "HosterRegistrations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SerializedConfig = table.Column<string>(
                        type: "TEXT",
                        maxLength: 4000,
                        nullable: false
                    ),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasUnreadableSecrets = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresCaptchaVerification = table.Column<bool>(
                        type: "INTEGER",
                        nullable: false
                    ),
                    HosterClassName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: false
                    ),
                    MaxParallelUploadsOverride = table.Column<int>(type: "INTEGER", nullable: true),
                    UploadSpeedLimitMegabytesPerSecond = table.Column<decimal>(
                        type: "TEXT",
                        precision: 10,
                        scale: 3,
                        nullable: true
                    ),
                    NumberOfHoursUntilReuploadOverride = table.Column<int>(
                        type: "INTEGER",
                        nullable: true
                    ),
                    ReuploadTriggerOverride = table.Column<int>(type: "INTEGER", nullable: true),
                    AlwaysReuploadAllFiles = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseForMirrorDownloads = table.Column<bool>(type: "INTEGER", nullable: false),
                    MirrorPriority = table.Column<int>(
                        type: "INTEGER",
                        nullable: false,
                        defaultValue: 100
                    ),
                    MirrorDownloadSpeedLimitMegabytesPerSecond = table.Column<decimal>(
                        type: "TEXT",
                        precision: 10,
                        scale: 3,
                        nullable: true
                    ),
                    UploadProxySelection = table.Column<int>(type: "INTEGER", nullable: false),
                    UploadProxyServerId = table.Column<int>(type: "INTEGER", nullable: true),
                    MirrorDownloadProxySelection = table.Column<int>(
                        type: "INTEGER",
                        nullable: false
                    ),
                    MirrorDownloadProxyServerId = table.Column<int>(
                        type: "INTEGER",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HosterRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HosterRegistrations_ProxyServers_MirrorDownloadProxyServerId",
                        column: x => x.MirrorDownloadProxyServerId,
                        principalTable: "ProxyServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_HosterRegistrations_ProxyServers_UploadProxyServerId",
                        column: x => x.UploadProxyServerId,
                        principalTable: "ProxyServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ImageHosterRegistrations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ImageHosterClassName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: false
                    ),
                    SerializedConfig = table.Column<string>(
                        type: "TEXT",
                        maxLength: 2000,
                        nullable: false
                    ),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasUnreadableSecrets = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProxySelection = table.Column<int>(type: "INTEGER", nullable: false),
                    ProxyServerId = table.Column<int>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageHosterRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageHosterRegistrations_ProxyServers_ProxyServerId",
                        column: x => x.ProxyServerId,
                        principalTable: "ProxyServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "LinkCrypterRegistrations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LinkCrypterClassName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 50,
                        nullable: false
                    ),
                    SerializedConfig = table.Column<string>(
                        type: "TEXT",
                        maxLength: 4000,
                        nullable: false
                    ),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasUnreadableSecrets = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProxySelection = table.Column<int>(type: "INTEGER", nullable: false),
                    ProxyServerId = table.Column<int>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkCrypterRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkCrypterRegistrations_ProxyServers_ProxyServerId",
                        column: x => x.ProxyServerId,
                        principalTable: "ProxyServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ProxyCategoryDefaults",
                columns: table => new
                {
                    ProxyCategory = table.Column<int>(type: "INTEGER", nullable: false),
                    ProxyServerId = table.Column<int>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProxyCategoryDefaults", x => x.ProxyCategory);
                    table.ForeignKey(
                        name: "FK_ProxyCategoryDefaults_ProxyServers_ProxyServerId",
                        column: x => x.ProxyServerId,
                        principalTable: "ProxyServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "QualityCheckRules",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QualityProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    RuleType = table.Column<int>(type: "INTEGER", nullable: false),
                    ParametersJson = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityCheckRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityCheckRules_QualityProfiles_QualityProfileId",
                        column: x => x.QualityProfileId,
                        principalTable: "QualityProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ReleaseGroups",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    EnableAutomaticReuploads = table.Column<bool>(type: "INTEGER", nullable: false),
                    NumberOfHoursUntilReupload = table.Column<int>(
                        type: "INTEGER",
                        nullable: false
                    ),
                    QualityProfileId = table.Column<int>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseGroups_QualityProfiles_QualityProfileId",
                        column: x => x.QualityProfileId,
                        principalTable: "QualityProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ReleaseCollections",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseContentType = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleaseGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PrimaryLanguageCode = table.Column<string>(
                        type: "TEXT",
                        maxLength: 2,
                        nullable: true
                    ),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                    MetadataCheckedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                    UploadsPostedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseCollections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseCollections_ReleaseGroups_ReleaseGroupId",
                        column: x => x.ReleaseGroupId,
                        principalTable: "ReleaseGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ReleaseTemplates",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ReleaseType = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleaseContentType = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleaseGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleaseCollectionDetectionMode = table.Column<int>(
                        type: "INTEGER",
                        nullable: false
                    ),
                    ReleaseCollectionPattern = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: true
                    ),
                    ReleaseCollectionKeyTemplate = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: true
                    ),
                    ReleaseCollectionNameTemplate = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseTemplates_ReleaseGroups_ReleaseGroupId",
                        column: x => x.ReleaseGroupId,
                        principalTable: "ReleaseGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "CollectionUploadSlots",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseCollectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordPolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpectedArchivePassword = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionUploadSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionUploadSlots_ReleaseCollections_ReleaseCollectionId",
                        column: x => x.ReleaseCollectionId,
                        principalTable: "ReleaseCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ReleaseCollectionMetadata",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseCollectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    MetadataDatabaseClassName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: false
                    ),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CoverUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    MetadataDatabaseUrl = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseCollectionMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseCollectionMetadata_ReleaseCollections_ReleaseCollectionId",
                        column: x => x.ReleaseCollectionId,
                        principalTable: "ReleaseCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Releases",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                    ReleaseType = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleaseContentType = table.Column<int>(type: "INTEGER", nullable: false),
                    PrimaryLanguageCode = table.Column<string>(
                        type: "TEXT",
                        maxLength: 2,
                        nullable: true
                    ),
                    ReleaseGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleaseCollectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReleaseFolderPath = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: true
                    ),
                    ReleaseInfoCheckedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                    MetadataCheckedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                    MediaMetadataExtractedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                    UploadsPostedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                    ExcludeFromAutoCleanup = table.Column<bool>(type: "INTEGER", nullable: false),
                    QualityGateState = table.Column<int>(type: "INTEGER", nullable: false),
                    QualityGateEvaluatedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Releases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Releases_ReleaseCollections_ReleaseCollectionId",
                        column: x => x.ReleaseCollectionId,
                        principalTable: "ReleaseCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_Releases_ReleaseGroups_ReleaseGroupId",
                        column: x => x.ReleaseGroupId,
                        principalTable: "ReleaseGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ArchiveConfigTemplates",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ArchiveFilesBasePath = table.Column<string>(
                        type: "TEXT",
                        maxLength: 300,
                        nullable: false
                    ),
                    ArchiverName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 200,
                        nullable: false
                    ),
                    ArchivePassword = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: true
                    ),
                    ArchiveFileSizeMb = table.Column<int>(type: "INTEGER", nullable: false),
                    UseReleaseNameAsArchiveName = table.Column<bool>(
                        type: "INTEGER",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveConfigTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchiveConfigTemplates_ReleaseTemplates_ReleaseTemplateId",
                        column: x => x.ReleaseTemplateId,
                        principalTable: "ReleaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "CollectionImageUploadConfigTemplates",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageHosterRegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionImageUploadConfigTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionImageUploadConfigTemplates_ImageHosterRegistrations_ImageHosterRegistrationId",
                        column: x => x.ImageHosterRegistrationId,
                        principalTable: "ImageHosterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_CollectionImageUploadConfigTemplates_ReleaseTemplates_ReleaseTemplateId",
                        column: x => x.ReleaseTemplateId,
                        principalTable: "ReleaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ImageUploadConfigTemplates",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageHosterRegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageUploadConfigTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageUploadConfigTemplates_ImageHosterRegistrations_ImageHosterRegistrationId",
                        column: x => x.ImageHosterRegistrationId,
                        principalTable: "ImageHosterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ImageUploadConfigTemplates_ReleaseTemplates_ReleaseTemplateId",
                        column: x => x.ReleaseTemplateId,
                        principalTable: "ReleaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ReleaseFolderAutomations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BasePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FolderNamePattern = table.Column<string>(
                        type: "TEXT",
                        maxLength: 200,
                        nullable: true
                    ),
                    PrimaryLanguageCode = table.Column<string>(
                        type: "TEXT",
                        maxLength: 2,
                        nullable: true
                    ),
                    ReleaseTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseFolderAutomations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseFolderAutomations_ReleaseTemplates_ReleaseTemplateId",
                        column: x => x.ReleaseTemplateId,
                        principalTable: "ReleaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "RemoteSourceAutomations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RemoteSourceRegistrationId = table.Column<int>(
                        type: "INTEGER",
                        nullable: false
                    ),
                    RemotePath = table.Column<string>(type: "text", nullable: false),
                    TargetPath = table.Column<string>(type: "text", nullable: false),
                    FolderNamePattern = table.Column<string>(
                        type: "TEXT",
                        maxLength: 200,
                        nullable: true
                    ),
                    ReleaseTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    PrimaryLanguageCode = table.Column<string>(
                        type: "TEXT",
                        maxLength: 2,
                        nullable: true
                    ),
                    KeepRawFiles = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExtractArchivesBeforeReleaseCreation = table.Column<bool>(
                        type: "INTEGER",
                        nullable: false
                    ),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IgnoreExistingOnFirstScan = table.Column<bool>(
                        type: "INTEGER",
                        nullable: false
                    ),
                    HasCompletedInitialScan = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteSourceAutomations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemoteSourceAutomations_ReleaseTemplates_ReleaseTemplateId",
                        column: x => x.ReleaseTemplateId,
                        principalTable: "ReleaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_RemoteSourceAutomations_RemoteSourceRegistrations_RemoteSourceRegistrationId",
                        column: x => x.RemoteSourceRegistrationId,
                        principalTable: "RemoteSourceRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ArchiveConfigs",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ArchiveFilesBasePath = table.Column<string>(
                        type: "TEXT",
                        maxLength: 300,
                        nullable: false
                    ),
                    ArchiverName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 200,
                        nullable: false
                    ),
                    ArchiveNamePrefix = table.Column<string>(
                        type: "TEXT",
                        maxLength: 200,
                        nullable: true
                    ),
                    ArchivePassword = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: true
                    ),
                    ArchiveFileSizeMb = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchiveConfigs_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ImageUploadConfigs",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReleaseCollectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImageHosterRegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageUploadConfigs", x => x.Id);
                    table.CheckConstraint(
                        "CK_ImageUploadConfig_Owner",
                        "(\"ReleaseId\" IS NOT NULL) <> (\"ReleaseCollectionId\" IS NOT NULL)"
                    );
                    table.ForeignKey(
                        name: "FK_ImageUploadConfigs_ImageHosterRegistrations_ImageHosterRegistrationId",
                        column: x => x.ImageHosterRegistrationId,
                        principalTable: "ImageHosterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ImageUploadConfigs_ReleaseCollections_ReleaseCollectionId",
                        column: x => x.ReleaseCollectionId,
                        principalTable: "ReleaseCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ImageUploadConfigs_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "PostedLocations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReleaseCollectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    DistributionSiteRegistrationId = table.Column<int>(
                        type: "INTEGER",
                        nullable: true
                    ),
                    ForumPostTemplateId = table.Column<int>(type: "INTEGER", nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                    ContentUpdatedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostedLocations", x => x.Id);
                    table.CheckConstraint(
                        "CK_PostedLocation_Owner",
                        "(\"ReleaseId\" IS NOT NULL) <> (\"ReleaseCollectionId\" IS NOT NULL)"
                    );
                    table.ForeignKey(
                        name: "FK_PostedLocations_DistributionSiteRegistrations_DistributionSiteRegistrationId",
                        column: x => x.DistributionSiteRegistrationId,
                        principalTable: "DistributionSiteRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_PostedLocations_ForumPostTemplates_ForumPostTemplateId",
                        column: x => x.ForumPostTemplateId,
                        principalTable: "ForumPostTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_PostedLocations_ReleaseCollections_ReleaseCollectionId",
                        column: x => x.ReleaseCollectionId,
                        principalTable: "ReleaseCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_PostedLocations_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ReleaseClassifications",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: true),
                    Season = table.Column<int>(type: "INTEGER", nullable: true),
                    Episode = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeEnd = table.Column<int>(type: "INTEGER", nullable: true),
                    ContentType = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentTypeSource = table.Column<int>(type: "INTEGER", nullable: false),
                    Platform = table.Column<int>(type: "INTEGER", nullable: false),
                    PlatformSource = table.Column<int>(type: "INTEGER", nullable: false),
                    Resolution = table.Column<int>(type: "INTEGER", nullable: false),
                    ResolutionSource = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceSource = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleaseGroupToken = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: true
                    ),
                    PrimaryLanguage = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: true
                    ),
                    LanguageSource = table.Column<int>(type: "INTEGER", nullable: false),
                    IsMultiLanguage = table.Column<bool>(type: "INTEGER", nullable: false),
                    ParserVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ClassifiedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseClassifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseClassifications_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ReleaseExternalIdentifiers",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseExternalIdentifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseExternalIdentifiers_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ReleaseInfos",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    NfoDatabaseClassName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: false
                    ),
                    ReleaseName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: false
                    ),
                    ReleaseDatabaseUrl = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: true
                    ),
                    SizeNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    SizeUnit = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    VideoType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AudioType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ContentKind = table.Column<int>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseInfos_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ReleaseMediaFiles",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelativePath = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: false
                    ),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    MediaInfoJson = table.Column<string>(type: "TEXT", nullable: false),
                    MediaInfoText = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseMediaFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseMediaFiles_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ReleaseMetadata",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    MetadataDatabaseClassName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: false
                    ),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Genre = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CoverUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    MetadataDatabaseUrl = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseMetadata_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ReleaseNfos",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseNfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseNfos_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ReleaseQualityIssues",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    RuleType = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseQualityIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseQualityIssues_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ArchiveConfigTemplateAdditionalArchiveContents",
                columns: table => new
                {
                    ArchiveConfigTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    AdditionalArchiveContentId = table.Column<int>(
                        type: "INTEGER",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_ArchiveConfigTemplateAdditionalArchiveContents",
                        x => new { x.ArchiveConfigTemplateId, x.AdditionalArchiveContentId }
                    );
                    table.ForeignKey(
                        name: "FK_ArchiveConfigTemplateAdditionalArchiveContents_AdditionalArchiveContents_AdditionalArchiveContentId",
                        column: x => x.AdditionalArchiveContentId,
                        principalTable: "AdditionalArchiveContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_ArchiveConfigTemplateAdditionalArchiveContents_ArchiveConfigTemplates_ArchiveConfigTemplateId",
                        column: x => x.ArchiveConfigTemplateId,
                        principalTable: "ArchiveConfigTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "UploadConfigTemplates",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    ArchiveConfigTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    HosterRegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PremiumOnlyDownload = table.Column<bool>(type: "INTEGER", nullable: false),
                    CollectionUploadSlotKey = table.Column<string>(
                        type: "TEXT",
                        maxLength: 200,
                        nullable: true
                    ),
                    CollectionUploadSlotName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 200,
                        nullable: true
                    ),
                    CollectionUploadSlotIsRequired = table.Column<bool>(
                        type: "INTEGER",
                        nullable: false
                    ),
                    CollectionUploadSlotPasswordPolicy = table.Column<int>(
                        type: "INTEGER",
                        nullable: false
                    ),
                    CollectionUploadSlotExpectedArchivePassword = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadConfigTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UploadConfigTemplates_ArchiveConfigTemplates_ArchiveConfigTemplateId",
                        column: x => x.ArchiveConfigTemplateId,
                        principalTable: "ArchiveConfigTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_UploadConfigTemplates_HosterRegistrations_HosterRegistrationId",
                        column: x => x.HosterRegistrationId,
                        principalTable: "HosterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_UploadConfigTemplates_ReleaseTemplates_ReleaseTemplateId",
                        column: x => x.ReleaseTemplateId,
                        principalTable: "ReleaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "RemoteSourceDownloads",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RemoteSourceAutomationId = table.Column<int>(type: "INTEGER", nullable: true),
                    RemoteSourceRegistrationId = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: false
                    ),
                    RemoteFolderPath = table.Column<string>(type: "text", nullable: false),
                    FolderName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: false
                    ),
                    LocalFolderPath = table.Column<string>(type: "text", nullable: false),
                    ReleaseTemplateId = table.Column<int>(type: "INTEGER", nullable: true),
                    PrimaryLanguageCode = table.Column<string>(
                        type: "TEXT",
                        maxLength: 2,
                        nullable: true
                    ),
                    KeepRawFiles = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExtractArchivesBeforeReleaseCreation = table.Column<bool>(
                        type: "INTEGER",
                        nullable: false
                    ),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    FileCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    LastChangedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: false
                    ),
                    DiscoveredAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: false
                    ),
                    StartedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: true),
                    CompletedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                    ArchivesExtractedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteSourceDownloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemoteSourceDownloads_ReleaseTemplates_ReleaseTemplateId",
                        column: x => x.ReleaseTemplateId,
                        principalTable: "ReleaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_RemoteSourceDownloads_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_RemoteSourceDownloads_RemoteSourceAutomations_RemoteSourceAutomationId",
                        column: x => x.RemoteSourceAutomationId,
                        principalTable: "RemoteSourceAutomations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_RemoteSourceDownloads_RemoteSourceRegistrations_RemoteSourceRegistrationId",
                        column: x => x.RemoteSourceRegistrationId,
                        principalTable: "RemoteSourceRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ArchiveConfigAdditionalArchiveContents",
                columns: table => new
                {
                    ArchiveConfigId = table.Column<int>(type: "INTEGER", nullable: false),
                    AdditionalArchiveContentId = table.Column<int>(
                        type: "INTEGER",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_ArchiveConfigAdditionalArchiveContents",
                        x => new { x.ArchiveConfigId, x.AdditionalArchiveContentId }
                    );
                    table.ForeignKey(
                        name: "FK_ArchiveConfigAdditionalArchiveContents_AdditionalArchiveContents_AdditionalArchiveContentId",
                        column: x => x.AdditionalArchiveContentId,
                        principalTable: "AdditionalArchiveContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_ArchiveConfigAdditionalArchiveContents_ArchiveConfigs_ArchiveConfigId",
                        column: x => x.ArchiveConfigId,
                        principalTable: "ArchiveConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Archives",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArchiveConfigId = table.Column<int>(type: "INTEGER", nullable: false),
                    ArchiveFolderPath = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: false
                    ),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                    ArchiveState = table.Column<int>(type: "INTEGER", nullable: false),
                    ArchiveFileSizeMb = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessages = table.Column<string>(type: "TEXT", nullable: false),
                    ReleaseFolderEntriesCopiedForPacking = table.Column<string>(
                        type: "TEXT",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Archives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Archives_ArchiveConfigs_ArchiveConfigId",
                        column: x => x.ArchiveConfigId,
                        principalTable: "ArchiveConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "UploadConfigs",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    HosterRegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    ArchiveConfigId = table.Column<int>(type: "INTEGER", nullable: false),
                    CollectionUploadSlotId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PremiumOnlyDownload = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UploadConfigs_ArchiveConfigs_ArchiveConfigId",
                        column: x => x.ArchiveConfigId,
                        principalTable: "ArchiveConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_UploadConfigs_CollectionUploadSlots_CollectionUploadSlotId",
                        column: x => x.CollectionUploadSlotId,
                        principalTable: "CollectionUploadSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_UploadConfigs_HosterRegistrations_HosterRegistrationId",
                        column: x => x.HosterRegistrationId,
                        principalTable: "HosterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_UploadConfigs_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ImageUploads",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImageUploadConfigId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UploadState = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessages = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageUploads_ImageUploadConfigs_ImageUploadConfigId",
                        column: x => x.ImageUploadConfigId,
                        principalTable: "ImageUploadConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ReleaseExternalInfos",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Urls = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseExternalInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseExternalInfos_ReleaseInfos_ReleaseInfoId",
                        column: x => x.ReleaseInfoId,
                        principalTable: "ReleaseInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "UploadConfigLinkCrypterTemplates",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UploadConfigTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkCrypterRegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerScope = table.Column<int>(type: "INTEGER", nullable: false),
                    Password = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EnableCaptcha = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableContainerDownload = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableClickAndLoad = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadConfigLinkCrypterTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UploadConfigLinkCrypterTemplates_LinkCrypterRegistrations_LinkCrypterRegistrationId",
                        column: x => x.LinkCrypterRegistrationId,
                        principalTable: "LinkCrypterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_UploadConfigLinkCrypterTemplates_UploadConfigTemplates_UploadConfigTemplateId",
                        column: x => x.UploadConfigTemplateId,
                        principalTable: "UploadConfigTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ArchiveFiles",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArchiveId = table.Column<int>(type: "INTEGER", nullable: false),
                    FullFileName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: false
                    ),
                    Md5Hash = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchiveFiles_Archives_ArchiveId",
                        column: x => x.ArchiveId,
                        principalTable: "Archives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "UploadConfigLinkCrypters",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UploadConfigId = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkCrypterRegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerScope = table.Column<int>(type: "INTEGER", nullable: false),
                    Password = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EnableCaptcha = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableContainerDownload = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableClickAndLoad = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadConfigLinkCrypters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UploadConfigLinkCrypters_LinkCrypterRegistrations_LinkCrypterRegistrationId",
                        column: x => x.LinkCrypterRegistrationId,
                        principalTable: "LinkCrypterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_UploadConfigLinkCrypters_UploadConfigs_UploadConfigId",
                        column: x => x.UploadConfigId,
                        principalTable: "UploadConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Uploads",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UploadConfigId = table.Column<int>(type: "INTEGER", nullable: false),
                    ArchiveId = table.Column<int>(type: "INTEGER", nullable: true),
                    HosterFolderId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: true),
                    UploadState = table.Column<int>(type: "INTEGER", nullable: false),
                    OnlineState = table.Column<int>(type: "INTEGER", nullable: false),
                    NotFullyOnlineSince = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                    FullyOfflineSince = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                    PremiumOnlyDownload = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessages = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Uploads_Archives_ArchiveId",
                        column: x => x.ArchiveId,
                        principalTable: "Archives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_Uploads_UploadConfigs_UploadConfigId",
                        column: x => x.UploadConfigId,
                        principalTable: "UploadConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ImageUploadUrls",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImageUploadId = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageSize = table.Column<int>(type: "INTEGER", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageUploadUrls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageUploadUrls_ImageUploads_ImageUploadId",
                        column: x => x.ImageUploadId,
                        principalTable: "ImageUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "LinkCrypterContainers",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    UploadConfigLinkCrypterId = table.Column<int>(type: "INTEGER", nullable: true),
                    UploadId = table.Column<int>(type: "INTEGER", nullable: true),
                    CollectionUploadSlotId = table.Column<int>(type: "INTEGER", nullable: true),
                    LinkCrypterRegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalReference = table.Column<string>(type: "TEXT", nullable: true),
                    StatusImageId = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: true
                    ),
                    ContainerUrl = table.Column<string>(
                        type: "TEXT",
                        maxLength: 200,
                        nullable: false
                    ),
                    Password = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EnableCaptcha = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableContainerDownload = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableClickAndLoad = table.Column<bool>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Errors = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkCrypterContainers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkCrypterContainers_CollectionUploadSlots_CollectionUploadSlotId",
                        column: x => x.CollectionUploadSlotId,
                        principalTable: "CollectionUploadSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_LinkCrypterContainers_LinkCrypterRegistrations_LinkCrypterRegistrationId",
                        column: x => x.LinkCrypterRegistrationId,
                        principalTable: "LinkCrypterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_LinkCrypterContainers_UploadConfigLinkCrypters_UploadConfigLinkCrypterId",
                        column: x => x.UploadConfigLinkCrypterId,
                        principalTable: "UploadConfigLinkCrypters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_LinkCrypterContainers_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "UploadedFiles",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UploadId = table.Column<int>(type: "INTEGER", nullable: false),
                    ArchiveFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    HosterFileLink = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: false
                    ),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    HosterFolderId = table.Column<string>(type: "TEXT", nullable: true),
                    Md5Hash = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ErrorMessages = table.Column<string>(type: "TEXT", nullable: false),
                    OnlineState = table.Column<int>(type: "INTEGER", nullable: false),
                    DownloadCount = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadedFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UploadedFiles_ArchiveFiles_ArchiveFileId",
                        column: x => x.ArchiveFileId,
                        principalTable: "ArchiveFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_UploadedFiles_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "LinkCrypterContainerSourceUploads",
                columns: table => new
                {
                    LinkCrypterContainerId = table.Column<int>(type: "INTEGER", nullable: false),
                    UploadId = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_LinkCrypterContainerSourceUploads",
                        x => new { x.LinkCrypterContainerId, x.UploadId }
                    );
                    table.ForeignKey(
                        name: "FK_LinkCrypterContainerSourceUploads_LinkCrypterContainers_LinkCrypterContainerId",
                        column: x => x.LinkCrypterContainerId,
                        principalTable: "LinkCrypterContainers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_LinkCrypterContainerSourceUploads_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: true),
                    NotificationSeverity = table.Column<int>(type: "INTEGER", nullable: false),
                    NotificationKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    UploadId = table.Column<int>(type: "INTEGER", nullable: true),
                    ArchiveId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    LinkCrypterContainerId = table.Column<int>(type: "INTEGER", nullable: true),
                    HosterRegistrationId = table.Column<int>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Archives_ArchiveId",
                        column: x => x.ArchiveId,
                        principalTable: "Archives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_Notifications_HosterRegistrations_HosterRegistrationId",
                        column: x => x.HosterRegistrationId,
                        principalTable: "HosterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_Notifications_LinkCrypterContainers_LinkCrypterContainerId",
                        column: x => x.LinkCrypterContainerId,
                        principalTable: "LinkCrypterContainers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_Notifications_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_Notifications_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "TelegramDeliveries",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NotificationId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                    DeliveredAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                    NextAttemptAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelegramDeliveries_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_AdditionalArchiveContents_Name",
                table: "AdditionalArchiveContents",
                column: "Name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationConfigurationOverrides_ConfigurationKey_PropertyName",
                table: "ApplicationConfigurationOverrides",
                columns: new[] { "ConfigurationKey", "PropertyName" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveConfigAdditionalArchiveContents_AdditionalArchiveContentId",
                table: "ArchiveConfigAdditionalArchiveContents",
                column: "AdditionalArchiveContentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveConfigs_ReleaseId",
                table: "ArchiveConfigs",
                column: "ReleaseId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveConfigTemplateAdditionalArchiveContents_AdditionalArchiveContentId",
                table: "ArchiveConfigTemplateAdditionalArchiveContents",
                column: "AdditionalArchiveContentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveConfigTemplates_ReleaseTemplateId",
                table: "ArchiveConfigTemplates",
                column: "ReleaseTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveFiles_ArchiveId",
                table: "ArchiveFiles",
                column: "ArchiveId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Archives_ArchiveConfigId",
                table: "Archives",
                column: "ArchiveConfigId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundTaskStates_Key",
                table: "BackgroundTaskStates",
                column: "Key",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_CollectionImageUploadConfigTemplates_ImageHosterRegistrationId",
                table: "CollectionImageUploadConfigTemplates",
                column: "ImageHosterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CollectionImageUploadConfigTemplates_ReleaseTemplateId",
                table: "CollectionImageUploadConfigTemplates",
                column: "ReleaseTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CollectionUploadSlots_ReleaseCollectionId_Key",
                table: "CollectionUploadSlots",
                columns: new[] { "ReleaseCollectionId", "Key" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ForumPostingRules_DistributionSiteRegistrationId_SortOrder",
                table: "ForumPostingRules",
                columns: new[] { "DistributionSiteRegistrationId", "SortOrder" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ForumPostingRules_ForumPostTemplateId",
                table: "ForumPostingRules",
                column: "ForumPostTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ForumPostTemplates_Name",
                table: "ForumPostTemplates",
                column: "Name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_HosterRegistrations_MirrorDownloadProxyServerId",
                table: "HosterRegistrations",
                column: "MirrorDownloadProxyServerId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_HosterRegistrations_UploadProxyServerId",
                table: "HosterRegistrations",
                column: "UploadProxyServerId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageHosterRegistrations_ProxyServerId",
                table: "ImageHosterRegistrations",
                column: "ProxyServerId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploadConfigs_ImageHosterRegistrationId",
                table: "ImageUploadConfigs",
                column: "ImageHosterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploadConfigs_ReleaseCollectionId",
                table: "ImageUploadConfigs",
                column: "ReleaseCollectionId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploadConfigs_ReleaseId",
                table: "ImageUploadConfigs",
                column: "ReleaseId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploadConfigTemplates_ImageHosterRegistrationId",
                table: "ImageUploadConfigTemplates",
                column: "ImageHosterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploadConfigTemplates_ReleaseTemplateId",
                table: "ImageUploadConfigTemplates",
                column: "ReleaseTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploads_ImageUploadConfigId",
                table: "ImageUploads",
                column: "ImageUploadConfigId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploadUrls_ImageUploadId",
                table: "ImageUploadUrls",
                column: "ImageUploadId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterContainers_CollectionUploadSlotId",
                table: "LinkCrypterContainers",
                column: "CollectionUploadSlotId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterContainers_LinkCrypterRegistrationId",
                table: "LinkCrypterContainers",
                column: "LinkCrypterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterContainers_UploadConfigLinkCrypterId",
                table: "LinkCrypterContainers",
                column: "UploadConfigLinkCrypterId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterContainers_UploadId",
                table: "LinkCrypterContainers",
                column: "UploadId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterContainerSourceUploads_UploadId",
                table: "LinkCrypterContainerSourceUploads",
                column: "UploadId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterRegistrations_ProxyServerId",
                table: "LinkCrypterRegistrations",
                column: "ProxyServerId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MediaDatabaseRegistrations_MediaDatabaseClassName",
                table: "MediaDatabaseRegistrations",
                column: "MediaDatabaseClassName",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_NfoDatabaseRegistrations_NfoDatabaseClassName",
                table: "NfoDatabaseRegistrations",
                column: "NfoDatabaseClassName",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ArchiveId",
                table: "Notifications",
                column: "ArchiveId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedAt_Id",
                table: "Notifications",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0],
                filter: "\"ResolvedAt\" IS NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_HosterRegistrationId",
                table: "Notifications",
                column: "HosterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_LinkCrypterContainerId",
                table: "Notifications",
                column: "LinkCrypterContainerId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ReleaseId",
                table: "Notifications",
                column: "ReleaseId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UploadId",
                table: "Notifications",
                column: "UploadId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PostedLocations_DistributionSiteRegistrationId",
                table: "PostedLocations",
                column: "DistributionSiteRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PostedLocations_ForumPostTemplateId",
                table: "PostedLocations",
                column: "ForumPostTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PostedLocations_ReleaseCollectionId",
                table: "PostedLocations",
                column: "ReleaseCollectionId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PostedLocations_ReleaseId",
                table: "PostedLocations",
                column: "ReleaseId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ProxyCategoryDefaults_ProxyServerId",
                table: "ProxyCategoryDefaults",
                column: "ProxyServerId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ProxyServers_Host_Port",
                table: "ProxyServers",
                columns: new[] { "Host", "Port" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ProxyServers_Name",
                table: "ProxyServers",
                column: "Name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_QualityCheckRules_QualityProfileId",
                table: "QualityCheckRules",
                column: "QualityProfileId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseClassifications_ReleaseId",
                table: "ReleaseClassifications",
                column: "ReleaseId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseCollectionMetadata_ReleaseCollectionId",
                table: "ReleaseCollectionMetadata",
                column: "ReleaseCollectionId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseCollections_ReleaseGroupId_Key",
                table: "ReleaseCollections",
                columns: new[] { "ReleaseGroupId", "Key" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseExternalIdentifiers_ReleaseId_Type_Value_Source",
                table: "ReleaseExternalIdentifiers",
                columns: new[] { "ReleaseId", "Type", "Value", "Source" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseExternalInfos_ReleaseInfoId",
                table: "ReleaseExternalInfos",
                column: "ReleaseInfoId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseFolderAutomations_ReleaseTemplateId",
                table: "ReleaseFolderAutomations",
                column: "ReleaseTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseFolderObservations_FolderPath",
                table: "ReleaseFolderObservations",
                column: "FolderPath",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseGroups_QualityProfileId",
                table: "ReleaseGroups",
                column: "QualityProfileId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseInfos_ReleaseId",
                table: "ReleaseInfos",
                column: "ReleaseId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseMediaFiles_ReleaseId",
                table: "ReleaseMediaFiles",
                column: "ReleaseId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseMetadata_ReleaseId",
                table: "ReleaseMetadata",
                column: "ReleaseId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseNfos_ReleaseId",
                table: "ReleaseNfos",
                column: "ReleaseId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseQualityIssues_ReleaseId",
                table: "ReleaseQualityIssues",
                column: "ReleaseId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Releases_QualityGateState",
                table: "Releases",
                column: "QualityGateState"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Releases_ReleaseCollectionId",
                table: "Releases",
                column: "ReleaseCollectionId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Releases_ReleaseFolderPath",
                table: "Releases",
                column: "ReleaseFolderPath"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Releases_ReleaseGroupId",
                table: "Releases",
                column: "ReleaseGroupId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseTemplates_ReleaseGroupId",
                table: "ReleaseTemplates",
                column: "ReleaseGroupId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSourceAutomations_ReleaseTemplateId",
                table: "RemoteSourceAutomations",
                column: "ReleaseTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSourceAutomations_RemoteSourceRegistrationId_Priority",
                table: "RemoteSourceAutomations",
                columns: new[] { "RemoteSourceRegistrationId", "Priority" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSourceDownloads_ReleaseId",
                table: "RemoteSourceDownloads",
                column: "ReleaseId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSourceDownloads_ReleaseTemplateId",
                table: "RemoteSourceDownloads",
                column: "ReleaseTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSourceDownloads_RemoteSourceAutomationId_State",
                table: "RemoteSourceDownloads",
                columns: new[] { "RemoteSourceAutomationId", "State" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSourceDownloads_RemoteSourceRegistrationId_RemoteFolderPath",
                table: "RemoteSourceDownloads",
                columns: new[] { "RemoteSourceRegistrationId", "RemoteFolderPath" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_TelegramDeliveries_DeliveredAt_NextAttemptAt",
                table: "TelegramDeliveries",
                columns: new[] { "DeliveredAt", "NextAttemptAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TelegramDeliveries_NotificationId",
                table: "TelegramDeliveries",
                column: "NotificationId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigLinkCrypters_LinkCrypterRegistrationId",
                table: "UploadConfigLinkCrypters",
                column: "LinkCrypterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigLinkCrypters_UploadConfigId",
                table: "UploadConfigLinkCrypters",
                column: "UploadConfigId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigLinkCrypterTemplates_LinkCrypterRegistrationId",
                table: "UploadConfigLinkCrypterTemplates",
                column: "LinkCrypterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigLinkCrypterTemplates_UploadConfigTemplateId",
                table: "UploadConfigLinkCrypterTemplates",
                column: "UploadConfigTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigs_ArchiveConfigId",
                table: "UploadConfigs",
                column: "ArchiveConfigId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigs_CollectionUploadSlotId",
                table: "UploadConfigs",
                column: "CollectionUploadSlotId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigs_HosterRegistrationId",
                table: "UploadConfigs",
                column: "HosterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigs_ReleaseId",
                table: "UploadConfigs",
                column: "ReleaseId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigTemplates_ArchiveConfigTemplateId",
                table: "UploadConfigTemplates",
                column: "ArchiveConfigTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigTemplates_HosterRegistrationId",
                table: "UploadConfigTemplates",
                column: "HosterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigTemplates_ReleaseTemplateId",
                table: "UploadConfigTemplates",
                column: "ReleaseTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadedFiles_ArchiveFileId",
                table: "UploadedFiles",
                column: "ArchiveFileId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadedFiles_UploadId",
                table: "UploadedFiles",
                column: "UploadId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_ArchiveId",
                table: "Uploads",
                column: "ArchiveId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UploadConfigId",
                table: "Uploads",
                column: "UploadConfigId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UploadState_OnlineState",
                table: "Uploads",
                columns: new[] { "UploadState", "OnlineState" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ApplicationConfigurationOverrides");

            migrationBuilder.DropTable(name: "ArchiveConfigAdditionalArchiveContents");

            migrationBuilder.DropTable(name: "ArchiveConfigTemplateAdditionalArchiveContents");

            migrationBuilder.DropTable(name: "BackgroundTaskStates");

            migrationBuilder.DropTable(name: "CollectionImageUploadConfigTemplates");

            migrationBuilder.DropTable(name: "ForumPostingRules");

            migrationBuilder.DropTable(name: "ImageUploadConfigTemplates");

            migrationBuilder.DropTable(name: "ImageUploadUrls");

            migrationBuilder.DropTable(name: "LinkCrypterContainerSourceUploads");

            migrationBuilder.DropTable(name: "MediaDatabaseRegistrations");

            migrationBuilder.DropTable(name: "NfoDatabaseRegistrations");

            migrationBuilder.DropTable(name: "PostedLocations");

            migrationBuilder.DropTable(name: "ProxyCategoryDefaults");

            migrationBuilder.DropTable(name: "QualityCheckRules");

            migrationBuilder.DropTable(name: "ReleaseClassifications");

            migrationBuilder.DropTable(name: "ReleaseCollectionMetadata");

            migrationBuilder.DropTable(name: "ReleaseExternalIdentifiers");

            migrationBuilder.DropTable(name: "ReleaseExternalInfos");

            migrationBuilder.DropTable(name: "ReleaseFolderAutomations");

            migrationBuilder.DropTable(name: "ReleaseFolderObservations");

            migrationBuilder.DropTable(name: "ReleaseMediaFiles");

            migrationBuilder.DropTable(name: "ReleaseMetadata");

            migrationBuilder.DropTable(name: "ReleaseNfos");

            migrationBuilder.DropTable(name: "ReleaseQualityIssues");

            migrationBuilder.DropTable(name: "RemoteSourceDownloads");

            migrationBuilder.DropTable(name: "TelegramConfigurations");

            migrationBuilder.DropTable(name: "TelegramDeliveries");

            migrationBuilder.DropTable(name: "UploadConfigLinkCrypterTemplates");

            migrationBuilder.DropTable(name: "UploadedFiles");

            migrationBuilder.DropTable(name: "AdditionalArchiveContents");

            migrationBuilder.DropTable(name: "ImageUploads");

            migrationBuilder.DropTable(name: "DistributionSiteRegistrations");

            migrationBuilder.DropTable(name: "ForumPostTemplates");

            migrationBuilder.DropTable(name: "ReleaseInfos");

            migrationBuilder.DropTable(name: "RemoteSourceAutomations");

            migrationBuilder.DropTable(name: "Notifications");

            migrationBuilder.DropTable(name: "UploadConfigTemplates");

            migrationBuilder.DropTable(name: "ArchiveFiles");

            migrationBuilder.DropTable(name: "ImageUploadConfigs");

            migrationBuilder.DropTable(name: "RemoteSourceRegistrations");

            migrationBuilder.DropTable(name: "LinkCrypterContainers");

            migrationBuilder.DropTable(name: "ArchiveConfigTemplates");

            migrationBuilder.DropTable(name: "ImageHosterRegistrations");

            migrationBuilder.DropTable(name: "UploadConfigLinkCrypters");

            migrationBuilder.DropTable(name: "Uploads");

            migrationBuilder.DropTable(name: "ReleaseTemplates");

            migrationBuilder.DropTable(name: "LinkCrypterRegistrations");

            migrationBuilder.DropTable(name: "Archives");

            migrationBuilder.DropTable(name: "UploadConfigs");

            migrationBuilder.DropTable(name: "ArchiveConfigs");

            migrationBuilder.DropTable(name: "CollectionUploadSlots");

            migrationBuilder.DropTable(name: "HosterRegistrations");

            migrationBuilder.DropTable(name: "Releases");

            migrationBuilder.DropTable(name: "ProxyServers");

            migrationBuilder.DropTable(name: "ReleaseCollections");

            migrationBuilder.DropTable(name: "ReleaseGroups");

            migrationBuilder.DropTable(name: "QualityProfiles");
        }
    }
}
