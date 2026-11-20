using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Qirsh.Ledger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ledger");

            migrationBuilder.CreateTable(
                name: "InboxState",
                schema: "ledger",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    Received = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceiveCount = table.Column<int>(type: "integer", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Consumed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxState", x => x.Id);
                    table.UniqueConstraint("AK_InboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                schema: "ledger",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Bank = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Last4 = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsIncome = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "merchant_rules",
                schema: "ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Pattern = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TimesApplied = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "ledger",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnqueueTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Headers = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    InboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    InboxConsumerId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DestinationAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ResponseAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FaultAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                        columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                        principalSchema: "ledger",
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalSchema: "ledger",
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                schema: "ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SmsHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Direction = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Merchant = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MerchantKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transactions_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "ledger",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "ledger",
                table: "categories",
                columns: new[] { "Id", "IsIncome", "Name" },
                values: new object[,]
                {
                    { new Guid("5eed0001-0000-4000-a000-000000000001"), false, "Groceries" },
                    { new Guid("5eed0001-0000-4000-a000-000000000002"), false, "Food & Delivery" },
                    { new Guid("5eed0001-0000-4000-a000-000000000003"), false, "Transport" },
                    { new Guid("5eed0001-0000-4000-a000-000000000004"), false, "Shopping" },
                    { new Guid("5eed0001-0000-4000-a000-000000000005"), false, "Bills & Utilities" },
                    { new Guid("5eed0001-0000-4000-a000-000000000006"), false, "Entertainment" },
                    { new Guid("5eed0001-0000-4000-a000-000000000007"), false, "Health" },
                    { new Guid("5eed0001-0000-4000-a000-000000000008"), false, "Cash" },
                    { new Guid("5eed0001-0000-4000-a000-000000000009"), false, "Transfers" },
                    { new Guid("5eed0001-0000-4000-a000-000000000010"), false, "Uncategorised" },
                    { new Guid("5eed0001-0000-4000-a000-000000000011"), true, "Income" }
                });

            migrationBuilder.InsertData(
                schema: "ledger",
                table: "merchant_rules",
                columns: new[] { "Id", "Category", "Pattern", "Source", "TimesApplied" },
                values: new object[,]
                {
                    { new Guid("5eed0002-0000-4000-a000-000000000001"), "Groceries", "CARREFOUR", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000002"), "Groceries", "SPINNEYS", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000003"), "Groceries", "SEOUDI", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000004"), "Groceries", "METRO MARKET", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000005"), "Groceries", "كارفور", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000006"), "Food & Delivery", "TALABAT", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000007"), "Food & Delivery", "ELMENUS", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000008"), "Food & Delivery", "UBER EATS", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000009"), "Food & Delivery", "COSTA", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000010"), "Transport", "UBER", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000011"), "Transport", "CAREEM", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000012"), "Transport", "SWVL", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000013"), "Transport", "SHELL", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000014"), "Transport", "اوبر", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000015"), "Shopping", "AMAZON", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000016"), "Shopping", "NOON", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000017"), "Shopping", "IKEA", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000018"), "Shopping", "BTECH", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000019"), "Bills & Utilities", "FAWRY", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000020"), "Bills & Utilities", "WE INTERNET", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000021"), "Bills & Utilities", "VODAFONE", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000022"), "Entertainment", "NETFLIX", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000023"), "Entertainment", "SPOTIFY", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000024"), "Entertainment", "STEAM", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000025"), "Health", "PHARMACY", "Keyword", 0 },
                    { new Guid("5eed0002-0000-4000-a000-000000000026"), "Health", "صيدلية", "Keyword", 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                schema: "ledger",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                schema: "ledger",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                schema: "ledger",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                schema: "ledger",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                schema: "ledger",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                schema: "ledger",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_Bank_Last4",
                schema: "ledger",
                table: "accounts",
                columns: new[] { "Bank", "Last4" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_categories_Name",
                schema: "ledger",
                table: "categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_merchant_rules_Pattern_Source",
                schema: "ledger",
                table: "merchant_rules",
                columns: new[] { "Pattern", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_AccountId_SmsHash",
                schema: "ledger",
                table: "transactions",
                columns: new[] { "AccountId", "SmsHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_Category_OccurredOn",
                schema: "ledger",
                table: "transactions",
                columns: new[] { "Category", "OccurredOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "merchant_rules",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "transactions",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "InboxState",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "OutboxState",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "accounts",
                schema: "ledger");
        }
    }
}
