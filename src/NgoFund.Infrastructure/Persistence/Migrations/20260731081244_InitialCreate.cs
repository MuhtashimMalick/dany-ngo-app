using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "text", nullable: true),
                    data_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_editable = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "application_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_zakat_eligible = table.Column<bool>(type: "boolean", nullable: false),
                    default_max_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    entity_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    changed_columns = table.Column<string[]>(type: "text[]", nullable: true),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    machine_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "donors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    donor_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    donor_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cnic = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ntn = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    membership_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    alternate_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_donors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fund_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_zakat = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fund_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "number_sequences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    current_value = table.Column<long>(type: "bigint", nullable: false),
                    padding = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_number_sequences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    designation = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "donations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    donation_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    donor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fund_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    donation_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    receipt_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bank_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    instrument_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    voided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_donations", x => x.id);
                    table.CheckConstraint("ck_donations_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "fk_donations_donors_donor_id",
                        column: x => x.donor_id,
                        principalTable: "donors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_donations_fund_categories_fund_category_id",
                        column: x => x.fund_category_id,
                        principalTable: "fund_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fund_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fund_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reference_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fund_transactions", x => x.id);
                    table.CheckConstraint("ck_fund_transactions_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "fk_fund_transactions_fund_categories_fund_category_id",
                        column: x => x.fund_category_id,
                        principalTable: "fund_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_claims_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "fk_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    device_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_by_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_claims_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_user_logins_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_user_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "applicants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    father_or_husband_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cnic = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    marital_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    alternate_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    occupation = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    monthly_income = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    dependents_count = table.Column<int>(type: "integer", nullable: true),
                    household_size = table.Column<int>(type: "integer", nullable: true),
                    photo_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_blacklisted = table.Column<bool>(type: "boolean", nullable: false),
                    blacklist_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applicants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    applicant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fund_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    approved_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    application_date = table.Column<DateOnly>(type: "date", nullable: false),
                    purpose = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications", x => x.id);
                    table.CheckConstraint("ck_applications_approved_amount_le_requested", "approved_amount IS NULL OR approved_amount <= requested_amount");
                    table.CheckConstraint("ck_applications_requested_amount_positive", "requested_amount > 0");
                    table.ForeignKey(
                        name: "fk_applications_applicants_applicant_id",
                        column: x => x.applicant_id,
                        principalTable: "applicants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_applications_application_categories_application_category_id",
                        column: x => x.application_category_id,
                        principalTable: "application_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_applications_fund_categories_fund_category_id",
                        column: x => x.fund_category_id,
                        principalTable: "fund_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "application_remarks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    remark = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_internal = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_remarks", x => x.id);
                    table.ForeignKey(
                        name: "fk_application_remarks_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_status_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_application_status_history_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fund_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    instrument_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bank_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    paid_to_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    paid_to_cnic = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    paid_to_relation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    voided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.CheckConstraint("ck_payments_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "fk_payments_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_fund_categories_fund_category_id",
                        column: x => x.fund_category_id,
                        principalTable: "fund_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    document_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    applicant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    donation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documents", x => x.id);
                    table.CheckConstraint("ck_documents_exactly_one_owner", "num_nonnulls(applicant_id, application_id, donation_id, payment_id) = 1");
                    table.ForeignKey(
                        name: "fk_documents_applicants_applicant_id",
                        column: x => x.applicant_id,
                        principalTable: "applicants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documents_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documents_donations_donation_id",
                        column: x => x.donation_id,
                        principalTable: "donations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documents_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "app_settings",
                columns: new[] { "id", "created_at", "created_by", "data_type", "description", "is_editable", "key", "updated_at", "updated_by", "value" },
                values: new object[,]
                {
                    { new Guid("02d81ee7-89fd-2e61-0042-2e816a59125d"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "number", "1-12; month the fiscal year starts (7 = July).", true, "FiscalYearStartMonth", null, null, "7" },
                    { new Guid("061ceecb-084e-f38d-7c54-c682dd3f8caf"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "string", "Text printed at the top of donation/payment receipts.", true, "ReceiptHeader", null, null, "" },
                    { new Guid("16ae64bd-8702-4733-0d1b-8fdb77e00c95"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "string", "ISO currency code used for all monetary display.", true, "Currency", null, null, "PKR" },
                    { new Guid("6d42cd70-1352-1a7b-55d1-b35669ecfce9"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "string", "Text printed at the bottom of donation/payment receipts.", true, "ReceiptFooter", null, null, "" },
                    { new Guid("cb327966-85f0-f397-f8a7-aa14739d3a32"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "string", "Organization name shown in the app header and on receipts.", true, "OrgName", null, null, "NGO Fund Management" }
                });

            migrationBuilder.InsertData(
                table: "application_categories",
                columns: new[] { "id", "code", "created_at", "created_by", "default_max_amount", "deleted_at", "deleted_by", "display_order", "is_active", "is_deleted", "is_zakat_eligible", "name", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("20b2ef82-a783-0ce4-c3d7-b668207696b6"), "OTHER", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, 7, true, false, false, "Other", null, null },
                    { new Guid("2ccde3bf-3225-d02c-5590-cf448f4a0051"), "ROZGAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, 6, true, false, false, "Rozgar/Business Help", null, null },
                    { new Guid("5ff221ba-81fd-4406-c688-a3c674de461f"), "SHAADI", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, 1, true, false, true, "Shaadi Fund Request", null, null },
                    { new Guid("67769b37-5a89-ca89-35c3-83b71b644283"), "EMERGENCY", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, 5, true, false, true, "Emergency Support", null, null },
                    { new Guid("9ee8c9a2-70f3-538e-f223-f669aa195adf"), "EDUCATION", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, 3, true, false, true, "Education Support", null, null },
                    { new Guid("dc44b36f-32d6-7a7c-4e60-48e145f0e774"), "HOUSE_RENT", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, 4, true, false, true, "House Rent/Help", null, null },
                    { new Guid("e9c9608a-c26e-2132-b11d-ef39ff4f3000"), "HEALTH", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, 2, true, false, true, "Health Fund Request", null, null }
                });

            migrationBuilder.InsertData(
                table: "fund_categories",
                columns: new[] { "id", "code", "created_at", "created_by", "deleted_at", "deleted_by", "description", "display_order", "is_active", "is_deleted", "is_zakat", "name", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("21da0e90-2de6-d0f2-9e8c-70f53fa8dbcb"), "ZAKAT", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Zakat-eligible donations, disbursed only to Zakat-eligible application categories.", 1, true, false, true, "Zakat Fund", null, null },
                    { new Guid("dcecee48-3bca-93ee-15ea-04cb1f140ef1"), "GENERAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "General donations, may fund any application category.", 2, true, false, false, "General Fund", null, null }
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "created_at", "created_by", "description", "display_name", "module" },
                values: new object[,]
                {
                    { new Guid("16fdc1ce-737e-6b2d-8caf-48f83f8ebb6d"), "donations.create", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Record Donations", "donations" },
                    { new Guid("1ea7ae4c-267b-2665-6d9c-95aef875ea82"), "applications.create", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Create Applications", "applications" },
                    { new Guid("23abd8ff-0228-14ff-a354-63c48043ef1b"), "fundcategories.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "View Fund Categories", "fundcategories" },
                    { new Guid("2512657b-b641-e381-6057-add2eaca4df3"), "settings.manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Manage Settings", "settings" },
                    { new Guid("270b3031-1aa9-3b41-9a31-af214c8a2ce8"), "users.create", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Create Users", "users" },
                    { new Guid("30e1b3a4-9661-1641-f772-0702f3abfb76"), "applications.approve", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Approve/Reject Applications", "applications" },
                    { new Guid("3161fb95-a303-92be-7aa7-4926eb2b4e76"), "dashboard.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "View Dashboard", "dashboard" },
                    { new Guid("35f27e03-b782-6b6c-8496-932a07d4620f"), "donations.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "View Donations", "donations" },
                    { new Guid("3cacce04-c54e-46ba-d65a-d3a1dbbe5439"), "applications.edit", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Edit Applications", "applications" },
                    { new Guid("45d4081f-59fd-5c1a-fb09-a36736653e2e"), "fundcategories.manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Manage Fund Categories", "fundcategories" },
                    { new Guid("4c3276f3-5833-0236-b3db-7e99c5401c05"), "donors.create", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Create Donors", "donors" },
                    { new Guid("5071c7ab-8091-44ee-173c-cc8ad99cd251"), "reports.export", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Export Reports", "reports" },
                    { new Guid("523b5077-a6c4-26a6-79ae-1f9f464aac90"), "applicants.edit", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Edit Applicants", "applicants" },
                    { new Guid("5d159e8a-5133-d5a3-7923-1121a45ee583"), "donations.void", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Void Donations", "donations" },
                    { new Guid("5d67ab96-8af5-7c16-8cfa-03f3dc2c60c8"), "applicationcategories.manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Manage Application Categories", "applicationcategories" },
                    { new Guid("6c8bdf71-5ecf-ee6a-571f-daee60d5e56b"), "donors.edit", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Edit Donors", "donors" },
                    { new Guid("6ceaff37-5213-6d5b-464b-5658764bbbf5"), "payments.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "View Payments", "payments" },
                    { new Guid("6e500c0b-c113-2ebb-3898-187001f164c3"), "applicants.delete", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Delete Applicants", "applicants" },
                    { new Guid("7021cb7f-0f43-95e5-5078-150bcfbaad27"), "donors.delete", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Delete Donors", "donors" },
                    { new Guid("74af56f2-58db-b9e1-17d3-a7c519e84796"), "reports.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "View Reports", "reports" },
                    { new Guid("828c5b04-9c91-6f1a-2bc6-84d9d63e7aeb"), "documents.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "View Documents", "documents" },
                    { new Guid("8412827f-5cf1-08f3-9b57-7c57d1302cfd"), "donors.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "View Donors", "donors" },
                    { new Guid("87541710-523b-e0e6-e35e-acb3aaafa0c2"), "users.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "View Users", "users" },
                    { new Guid("8d991300-4f0f-fe89-ae4a-1987455adea6"), "donations.edit", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Edit Donations", "donations" },
                    { new Guid("8e0b0928-c3ab-9ebc-e03b-b5e2214db1f8"), "roles.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "View Roles", "roles" },
                    { new Guid("9c55f70e-3ba6-0bd3-26d0-264a21b2d7d1"), "users.delete", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Delete Users", "users" },
                    { new Guid("9e35778a-c8e4-5f19-4db3-77be6cb5bae4"), "documents.upload", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Upload Documents", "documents" },
                    { new Guid("aa784e35-1601-eab5-0994-06f0f2ea2635"), "settings.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "View Settings", "settings" },
                    { new Guid("b57ecef7-0d27-e4c6-3424-fd0363294d43"), "applicants.create", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Create Applicants", "applicants" },
                    { new Guid("ce4b9565-0a40-b007-b000-805fa2c189fd"), "users.edit", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Edit Users", "users" },
                    { new Guid("d2da11b2-6782-d766-9e3e-abe9ee313a80"), "documents.delete", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Delete Documents", "documents" },
                    { new Guid("de5fa8b0-1b3c-5067-2f7c-99f2d3d9849e"), "payments.void", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Void Payments", "payments" },
                    { new Guid("e30c9ea9-40b0-defe-c93c-2312754671f3"), "applications.review", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Review Applications", "applications" },
                    { new Guid("e9d6c2e4-4c7a-9623-4c5d-ea01f69902da"), "roles.manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Manage Roles & Permissions", "roles" },
                    { new Guid("f2669f40-2156-549d-dbcd-8d75c9088dc4"), "applications.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "View Applications", "applications" },
                    { new Guid("f3a723d1-0567-af8f-2532-45eee1f5c62c"), "payments.create", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Record Payments", "payments" },
                    { new Guid("f48ef59e-7ea5-9529-41b0-12e42456847b"), "auditlogs.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "View Audit Logs", "auditlogs" },
                    { new Guid("f57aed8e-febf-717b-b59a-a71bc03a9b2d"), "applicationcategories.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "View Application Categories", "applicationcategories" },
                    { new Guid("f996e26f-42a0-01a7-3c83-646e953d9042"), "applicants.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "View Applicants", "applicants" }
                });

            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "id", "concurrency_stamp", "description", "is_system", "name", "normalized_name" },
                values: new object[,]
                {
                    { new Guid("1bcca810-9298-8bb5-862d-b870025e4704"), "2a841c6e-0695-bc49-1510-52a0eae436a4", "Manages donors, donations, payments and financial reports.", true, "AccountsManager", "ACCOUNTSMANAGER" },
                    { new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4"), "d1c33712-6c45-9a25-4f2b-1c8d857dda5f", "Read-only access across the system.", true, "Viewer", "VIEWER" },
                    { new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad"), "4499e3a3-8694-119c-51c6-b29946b38d22", "Full operational access; cannot manage roles/permissions or system settings.", true, "Admin", "ADMIN" },
                    { new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c"), "b9b79ee6-4b01-21ac-68b0-a47b87b0f437", "Creates and edits applicants and applications; no financial or approval access.", true, "DataEntryOperator", "DATAENTRYOPERATOR" },
                    { new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0"), "12d96831-21b4-9978-3fe7-f74343fc44c0", "Full system access, including role/permission and settings management.", true, "SuperAdmin", "SUPERADMIN" }
                });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("16fdc1ce-737e-6b2d-8caf-48f83f8ebb6d"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("23abd8ff-0228-14ff-a354-63c48043ef1b"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("3161fb95-a303-92be-7aa7-4926eb2b4e76"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("35f27e03-b782-6b6c-8496-932a07d4620f"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("45d4081f-59fd-5c1a-fb09-a36736653e2e"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("4c3276f3-5833-0236-b3db-7e99c5401c05"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("5071c7ab-8091-44ee-173c-cc8ad99cd251"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("5d159e8a-5133-d5a3-7923-1121a45ee583"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("6c8bdf71-5ecf-ee6a-571f-daee60d5e56b"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("6ceaff37-5213-6d5b-464b-5658764bbbf5"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("7021cb7f-0f43-95e5-5078-150bcfbaad27"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("74af56f2-58db-b9e1-17d3-a7c519e84796"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("828c5b04-9c91-6f1a-2bc6-84d9d63e7aeb"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("8412827f-5cf1-08f3-9b57-7c57d1302cfd"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("8d991300-4f0f-fe89-ae4a-1987455adea6"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("9e35778a-c8e4-5f19-4db3-77be6cb5bae4"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("d2da11b2-6782-d766-9e3e-abe9ee313a80"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("de5fa8b0-1b3c-5067-2f7c-99f2d3d9849e"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("f2669f40-2156-549d-dbcd-8d75c9088dc4"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("f3a723d1-0567-af8f-2532-45eee1f5c62c"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("23abd8ff-0228-14ff-a354-63c48043ef1b"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") },
                    { new Guid("3161fb95-a303-92be-7aa7-4926eb2b4e76"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") },
                    { new Guid("35f27e03-b782-6b6c-8496-932a07d4620f"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") },
                    { new Guid("6ceaff37-5213-6d5b-464b-5658764bbbf5"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") },
                    { new Guid("74af56f2-58db-b9e1-17d3-a7c519e84796"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") },
                    { new Guid("828c5b04-9c91-6f1a-2bc6-84d9d63e7aeb"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") },
                    { new Guid("8412827f-5cf1-08f3-9b57-7c57d1302cfd"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") },
                    { new Guid("87541710-523b-e0e6-e35e-acb3aaafa0c2"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") },
                    { new Guid("8e0b0928-c3ab-9ebc-e03b-b5e2214db1f8"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") },
                    { new Guid("aa784e35-1601-eab5-0994-06f0f2ea2635"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") },
                    { new Guid("f2669f40-2156-549d-dbcd-8d75c9088dc4"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") },
                    { new Guid("f48ef59e-7ea5-9529-41b0-12e42456847b"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") },
                    { new Guid("f57aed8e-febf-717b-b59a-a71bc03a9b2d"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") },
                    { new Guid("f996e26f-42a0-01a7-3c83-646e953d9042"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") },
                    { new Guid("16fdc1ce-737e-6b2d-8caf-48f83f8ebb6d"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("1ea7ae4c-267b-2665-6d9c-95aef875ea82"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("23abd8ff-0228-14ff-a354-63c48043ef1b"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("270b3031-1aa9-3b41-9a31-af214c8a2ce8"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("30e1b3a4-9661-1641-f772-0702f3abfb76"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("3161fb95-a303-92be-7aa7-4926eb2b4e76"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("35f27e03-b782-6b6c-8496-932a07d4620f"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("3cacce04-c54e-46ba-d65a-d3a1dbbe5439"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("45d4081f-59fd-5c1a-fb09-a36736653e2e"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("4c3276f3-5833-0236-b3db-7e99c5401c05"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("5071c7ab-8091-44ee-173c-cc8ad99cd251"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("523b5077-a6c4-26a6-79ae-1f9f464aac90"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("5d159e8a-5133-d5a3-7923-1121a45ee583"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("5d67ab96-8af5-7c16-8cfa-03f3dc2c60c8"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("6c8bdf71-5ecf-ee6a-571f-daee60d5e56b"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("6ceaff37-5213-6d5b-464b-5658764bbbf5"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("6e500c0b-c113-2ebb-3898-187001f164c3"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("7021cb7f-0f43-95e5-5078-150bcfbaad27"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("74af56f2-58db-b9e1-17d3-a7c519e84796"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("828c5b04-9c91-6f1a-2bc6-84d9d63e7aeb"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("8412827f-5cf1-08f3-9b57-7c57d1302cfd"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("87541710-523b-e0e6-e35e-acb3aaafa0c2"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("8d991300-4f0f-fe89-ae4a-1987455adea6"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("8e0b0928-c3ab-9ebc-e03b-b5e2214db1f8"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("9c55f70e-3ba6-0bd3-26d0-264a21b2d7d1"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("9e35778a-c8e4-5f19-4db3-77be6cb5bae4"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("aa784e35-1601-eab5-0994-06f0f2ea2635"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("b57ecef7-0d27-e4c6-3424-fd0363294d43"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("ce4b9565-0a40-b007-b000-805fa2c189fd"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("d2da11b2-6782-d766-9e3e-abe9ee313a80"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("de5fa8b0-1b3c-5067-2f7c-99f2d3d9849e"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("e30c9ea9-40b0-defe-c93c-2312754671f3"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("f2669f40-2156-549d-dbcd-8d75c9088dc4"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("f3a723d1-0567-af8f-2532-45eee1f5c62c"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("f48ef59e-7ea5-9529-41b0-12e42456847b"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("f57aed8e-febf-717b-b59a-a71bc03a9b2d"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("f996e26f-42a0-01a7-3c83-646e953d9042"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("1ea7ae4c-267b-2665-6d9c-95aef875ea82"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") },
                    { new Guid("3161fb95-a303-92be-7aa7-4926eb2b4e76"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") },
                    { new Guid("35f27e03-b782-6b6c-8496-932a07d4620f"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") },
                    { new Guid("3cacce04-c54e-46ba-d65a-d3a1dbbe5439"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") },
                    { new Guid("523b5077-a6c4-26a6-79ae-1f9f464aac90"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") },
                    { new Guid("828c5b04-9c91-6f1a-2bc6-84d9d63e7aeb"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") },
                    { new Guid("8412827f-5cf1-08f3-9b57-7c57d1302cfd"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") },
                    { new Guid("9e35778a-c8e4-5f19-4db3-77be6cb5bae4"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") },
                    { new Guid("b57ecef7-0d27-e4c6-3424-fd0363294d43"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") },
                    { new Guid("f2669f40-2156-549d-dbcd-8d75c9088dc4"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") },
                    { new Guid("f996e26f-42a0-01a7-3c83-646e953d9042"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") },
                    { new Guid("16fdc1ce-737e-6b2d-8caf-48f83f8ebb6d"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("1ea7ae4c-267b-2665-6d9c-95aef875ea82"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("23abd8ff-0228-14ff-a354-63c48043ef1b"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("2512657b-b641-e381-6057-add2eaca4df3"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("270b3031-1aa9-3b41-9a31-af214c8a2ce8"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("30e1b3a4-9661-1641-f772-0702f3abfb76"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("3161fb95-a303-92be-7aa7-4926eb2b4e76"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("35f27e03-b782-6b6c-8496-932a07d4620f"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("3cacce04-c54e-46ba-d65a-d3a1dbbe5439"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("45d4081f-59fd-5c1a-fb09-a36736653e2e"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("4c3276f3-5833-0236-b3db-7e99c5401c05"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("5071c7ab-8091-44ee-173c-cc8ad99cd251"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("523b5077-a6c4-26a6-79ae-1f9f464aac90"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("5d159e8a-5133-d5a3-7923-1121a45ee583"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("5d67ab96-8af5-7c16-8cfa-03f3dc2c60c8"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("6c8bdf71-5ecf-ee6a-571f-daee60d5e56b"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("6ceaff37-5213-6d5b-464b-5658764bbbf5"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("6e500c0b-c113-2ebb-3898-187001f164c3"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("7021cb7f-0f43-95e5-5078-150bcfbaad27"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("74af56f2-58db-b9e1-17d3-a7c519e84796"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("828c5b04-9c91-6f1a-2bc6-84d9d63e7aeb"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("8412827f-5cf1-08f3-9b57-7c57d1302cfd"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("87541710-523b-e0e6-e35e-acb3aaafa0c2"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("8d991300-4f0f-fe89-ae4a-1987455adea6"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("8e0b0928-c3ab-9ebc-e03b-b5e2214db1f8"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("9c55f70e-3ba6-0bd3-26d0-264a21b2d7d1"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("9e35778a-c8e4-5f19-4db3-77be6cb5bae4"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("aa784e35-1601-eab5-0994-06f0f2ea2635"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("b57ecef7-0d27-e4c6-3424-fd0363294d43"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("ce4b9565-0a40-b007-b000-805fa2c189fd"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("d2da11b2-6782-d766-9e3e-abe9ee313a80"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("de5fa8b0-1b3c-5067-2f7c-99f2d3d9849e"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("e30c9ea9-40b0-defe-c93c-2312754671f3"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("e9d6c2e4-4c7a-9623-4c5d-ea01f69902da"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("f2669f40-2156-549d-dbcd-8d75c9088dc4"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("f3a723d1-0567-af8f-2532-45eee1f5c62c"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("f48ef59e-7ea5-9529-41b0-12e42456847b"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("f57aed8e-febf-717b-b59a-a71bc03a9b2d"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("f996e26f-42a0-01a7-3c83-646e953d9042"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_settings_key",
                table: "app_settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_applicants_cnic",
                table: "applicants",
                column: "cnic",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_applicants_full_name",
                table: "applicants",
                column: "full_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_applicants_membership_number",
                table: "applicants",
                column: "membership_number",
                unique: true,
                filter: "membership_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_applicants_phone",
                table: "applicants",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "ix_applicants_photo_document_id",
                table: "applicants",
                column: "photo_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_application_categories_code",
                table: "application_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_application_remarks_application_id",
                table: "application_remarks",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_application_status_history_application_id",
                table: "application_status_history",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_applicant_id",
                table: "applications",
                column: "applicant_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_application_category_id",
                table: "applications",
                column: "application_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_application_date",
                table: "applications",
                column: "application_date");

            migrationBuilder.CreateIndex(
                name: "ix_applications_application_number",
                table: "applications",
                column: "application_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_applications_fund_category_id",
                table: "applications",
                column: "fund_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_status",
                table: "applications",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_name_entity_id",
                table: "audit_logs",
                columns: new[] { "entity_name", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_occurred_at",
                table: "audit_logs",
                column: "occurred_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_applicant_id",
                table: "documents",
                column: "applicant_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_application_id",
                table: "documents",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_donation_id",
                table: "documents",
                column: "donation_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_payment_id",
                table: "documents",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_storage_key",
                table: "documents",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_donations_donation_date",
                table: "donations",
                column: "donation_date");

            migrationBuilder.CreateIndex(
                name: "ix_donations_donation_number",
                table: "donations",
                column: "donation_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_donations_donor_id",
                table: "donations",
                column: "donor_id");

            migrationBuilder.CreateIndex(
                name: "ix_donations_fund_category_id",
                table: "donations",
                column: "fund_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_donors_cnic",
                table: "donors",
                column: "cnic",
                unique: true,
                filter: "cnic IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_donors_donor_code",
                table: "donors",
                column: "donor_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_donors_membership_number",
                table: "donors",
                column: "membership_number",
                unique: true,
                filter: "membership_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_donors_phone",
                table: "donors",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "ix_fund_categories_code",
                table: "fund_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fund_transactions_fund_category_id",
                table: "fund_transactions",
                column: "fund_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_fund_transactions_reference_type_reference_id",
                table: "fund_transactions",
                columns: new[] { "reference_type", "reference_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fund_transactions_transaction_date",
                table: "fund_transactions",
                column: "transaction_date");

            migrationBuilder.CreateIndex(
                name: "ix_number_sequences_entity_type_year",
                table: "number_sequences",
                columns: new[] { "entity_type", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_application_id",
                table: "payments",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_fund_category_id",
                table: "payments",
                column: "fund_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_payment_date",
                table: "payments",
                column: "payment_date");

            migrationBuilder.CreateIndex(
                name: "ix_payments_payment_number",
                table: "payments",
                column: "payment_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_permissions_code",
                table: "permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_claims_role_id",
                table: "role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission_id",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_claims_user_id",
                table: "user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_logins_user_id",
                table: "user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "users",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "users",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_applicants_documents_photo_document_id",
                table: "applicants",
                column: "photo_document_id",
                principalTable: "documents",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_applicants_documents_photo_document_id",
                table: "applicants");

            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "application_remarks");

            migrationBuilder.DropTable(
                name: "application_status_history");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "fund_transactions");

            migrationBuilder.DropTable(
                name: "number_sequences");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "role_claims");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "user_claims");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "user_tokens");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "donations");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "donors");

            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "applicants");

            migrationBuilder.DropTable(
                name: "application_categories");

            migrationBuilder.DropTable(
                name: "fund_categories");
        }
    }
}
