using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Workshop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "body_panels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    description_gr = table.Column<string>(type: "text", nullable: false),
                    description_en = table.Column<string>(type: "text", nullable: true),
                    category = table.Column<int>(type: "integer", nullable: false),
                    side = table.Column<int>(type: "integer", nullable: false),
                    diagram_x = table.Column<decimal>(type: "numeric", nullable: true),
                    diagram_y = table.Column<decimal>(type: "numeric", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_body_panels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    address_line = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    postal_code = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "company_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    address_line = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    postal_code = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    vat_number = table.Column<string>(type: "text", nullable: false),
                    tax_office = table.Column<string>(type: "text", nullable: true),
                    logo_path = table.Column<string>(type: "text", nullable: true),
                    default_vat_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_type = table.Column<int>(type: "integer", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: true),
                    last_name = table.Column<string>(type: "text", nullable: true),
                    company_name = table.Column<string>(type: "text", nullable: true),
                    vat_number = table.Column<string>(type: "text", nullable: true),
                    tax_office = table.Column<string>(type: "text", nullable: true),
                    id_number = table.Column<string>(type: "text", nullable: true),
                    mobile_phone = table.Column<string>(type: "text", nullable: false),
                    secondary_phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    address_line = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    postal_code = table.Column<string>(type: "text", nullable: true),
                    gdpr_consent = table.Column<bool>(type: "boolean", nullable: false),
                    gdpr_consent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "insurance_companies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    vat_number = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    address_line = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_insurance_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_catalogs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name_gr = table.Column<string>(type: "text", nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: true),
                    default_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_catalogs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    vat_number = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    address_line = table.Column<string>(type: "text", nullable: true),
                    contact_person = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suppliers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
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
                    table.PrimaryKey("pk_asp_net_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "body_panel_operations",
                columns: table => new
                {
                    body_panel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_body_panel_operations", x => new { x.body_panel_id, x.operation });
                    table.ForeignKey(
                        name: "fk_body_panel_operations_body_panels_body_panel_id",
                        column: x => x.body_panel_id,
                        principalTable: "body_panels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_warehouses", x => x.id);
                    table.ForeignKey(
                        name: "fk_warehouses_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "adjusters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    insurance_company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_adjusters", x => x.id);
                    table.ForeignKey(
                        name: "fk_adjusters_insurance_companies_insurance_company_id",
                        column: x => x.insurance_company_id,
                        principalTable: "insurance_companies",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "assessors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    license_number = table.Column<string>(type: "text", nullable: true),
                    insurance_company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessors", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessors_insurance_companies_insurance_company_id",
                        column: x => x.insurance_company_id,
                        principalTable: "insurance_companies",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plate_number = table.Column<string>(type: "text", nullable: false),
                    vin = table.Column<string>(type: "text", nullable: true),
                    brand = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<string>(type: "text", nullable: true),
                    year = table.Column<int>(type: "integer", nullable: true),
                    color = table.Column<string>(type: "text", nullable: true),
                    fuel_type = table.Column<int>(type: "integer", nullable: true),
                    mileage = table.Column<int>(type: "integer", nullable: true),
                    insurance_company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    policy_number = table.Column<string>(type: "text", nullable: true),
                    insurance_expiration = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicles", x => x.id);
                    table.ForeignKey(
                        name: "fk_vehicles_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vehicles_insurance_companies_insurance_company_id",
                        column: x => x.insurance_company_id,
                        principalTable: "insurance_companies",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    portal_audience = table.Column<int>(type: "integer", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    insurance_company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    language = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("pk_asp_net_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_users_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_asp_net_users_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_asp_net_users_insurance_companies_insurance_company_id",
                        column: x => x.insurance_company_id,
                        principalTable: "insurance_companies",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_asp_net_users_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
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
                    table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_user_claims_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_asp_net_user_logins_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_name = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<int>(type: "integer", nullable: false),
                    changes = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "insurance_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_number = table.Column<string>(type: "text", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_number = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: true),
                    assigned_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assessor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    adjuster_id = table.Column<Guid>(type: "uuid", nullable: true),
                    driver_first_name = table.Column<string>(type: "text", nullable: true),
                    driver_last_name = table.Column<string>(type: "text", nullable: true),
                    driver_phone = table.Column<string>(type: "text", nullable: true),
                    driver_email = table.Column<string>(type: "text", nullable: true),
                    accident_date = table.Column<DateOnly>(type: "date", nullable: true),
                    mileage_at_assessment = table.Column<int>(type: "integer", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_insurance_cases", x => x.id);
                    table.ForeignKey(
                        name: "fk_insurance_cases_adjusters_adjuster_id",
                        column: x => x.adjuster_id,
                        principalTable: "adjusters",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_insurance_cases_assessors_assessor_id",
                        column: x => x.assessor_id,
                        principalTable: "assessors",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_insurance_cases_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_insurance_cases_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_insurance_cases_insurance_companies_insurance_company_id",
                        column: x => x.insurance_company_id,
                        principalTable: "insurance_companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_insurance_cases_users_assigned_user_id",
                        column: x => x.assigned_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_insurance_cases_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "retail_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_number = table.Column<string>(type: "text", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    assigned_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    work_type = table.Column<string>(type: "text", nullable: false),
                    final_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_with_vat = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retail_cases", x => x.id);
                    table.ForeignKey(
                        name: "fk_retail_cases_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_retail_cases_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_retail_cases_users_assigned_user_id",
                        column: x => x.assigned_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_retail_cases_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    labor_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    parts_required = table.Column<bool>(type: "boolean", nullable: false),
                    parts_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    paint_materials_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    total_estimated_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    agreed_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    agreement_date = table.Column<DateOnly>(type: "date", nullable: false),
                    intermediate_inspection = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessments", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessments_insurance_cases_insurance_case_id",
                        column: x => x.insurance_case_id,
                        principalTable: "insurance_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "insurance_approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    liability_accepted = table.Column<bool>(type: "boolean", nullable: false),
                    customer_participation = table.Column<bool>(type: "boolean", nullable: false),
                    participation_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    approved_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    approval_date = table.Column<DateOnly>(type: "date", nullable: false),
                    approval_status = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_insurance_approvals", x => x.id);
                    table.ForeignKey(
                        name: "fk_insurance_approvals_insurance_cases_insurance_case_id",
                        column: x => x.insurance_case_id,
                        principalTable: "insurance_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_insurance_approvals_insurance_companies_insurance_company_id",
                        column: x => x.insurance_company_id,
                        principalTable: "insurance_companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quotes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_number = table.Column<string>(type: "text", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    responsible_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    labor_subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    parts_subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    labor_discount_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    parts_discount_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    customer_participation = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    pdf_path = table.Column<string>(type: "text", nullable: true),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quotes", x => x.id);
                    table.ForeignKey(
                        name: "fk_quotes_insurance_cases_insurance_case_id",
                        column: x => x.insurance_case_id,
                        principalTable: "insurance_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_quotes_users_responsible_user_id",
                        column: x => x.responsible_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "repairs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    scheduled_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completion_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    technician_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    intermediate_inspection_done = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repairs", x => x.id);
                    table.ForeignKey(
                        name: "fk_repairs_insurance_cases_insurance_case_id",
                        column: x => x.insurance_case_id,
                        principalTable: "insurance_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_repairs_users_technician_id",
                        column: x => x.technician_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "case_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    retail_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_status = table.Column<string>(type: "text", nullable: true),
                    to_status = table.Column<string>(type: "text", nullable: false),
                    triggered_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_case_events_insurance_cases_insurance_case_id",
                        column: x => x.insurance_case_id,
                        principalTable: "insurance_cases",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_case_events_retail_cases_retail_case_id",
                        column: x => x.retail_case_id,
                        principalTable: "retail_cases",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_case_events_users_triggered_by_id",
                        column: x => x.triggered_by_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    retail_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_type = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    file_path = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sent_to_insurance = table.Column<bool>(type: "boolean", nullable: false),
                    sent_to_insurance_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_documents_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_documents_insurance_cases_insurance_case_id",
                        column: x => x.insurance_case_id,
                        principalTable: "insurance_cases",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_documents_retail_cases_retail_case_id",
                        column: x => x.retail_case_id,
                        principalTable: "retail_cases",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_documents_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_documents_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    retail_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_method = table.Column<int>(type: "integer", nullable: false),
                    payer = table.Column<string>(type: "text", nullable: true),
                    reference_number = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_payments_insurance_cases_insurance_case_id",
                        column: x => x.insurance_case_id,
                        principalTable: "insurance_cases",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_payments_retail_cases_retail_case_id",
                        column: x => x.retail_case_id,
                        principalTable: "retail_cases",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "retail_part_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retail_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    destination_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    part_type = table.Column<int>(type: "integer", nullable: false),
                    part_name = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    received_status = table.Column<int>(type: "integer", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    storage_location = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retail_part_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_retail_part_lines_branches_destination_branch_id",
                        column: x => x.destination_branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_retail_part_lines_retail_cases_retail_case_id",
                        column: x => x.retail_case_id,
                        principalTable: "retail_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_retail_part_lines_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_retail_part_lines_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "retail_repairs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retail_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    scheduled_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completion_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    technician_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retail_repairs", x => x.id);
                    table.ForeignKey(
                        name: "fk_retail_repairs_retail_cases_retail_case_id",
                        column: x => x.retail_case_id,
                        principalTable: "retail_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_retail_repairs_users_technician_id",
                        column: x => x.technician_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "insurance_part_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    destination_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    part_type = table.Column<int>(type: "integer", nullable: false),
                    part_name = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    availability_status = table.Column<int>(type: "integer", nullable: false),
                    insurance_approved = table.Column<bool>(type: "boolean", nullable: false),
                    ordered = table.Column<bool>(type: "boolean", nullable: false),
                    order_date = table.Column<DateOnly>(type: "date", nullable: true),
                    received_status = table.Column<int>(type: "integer", nullable: false),
                    received_date = table.Column<DateOnly>(type: "date", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    storage_location = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_insurance_part_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_insurance_part_lines_assessments_assessment_id",
                        column: x => x.assessment_id,
                        principalTable: "assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_insurance_part_lines_branches_destination_branch_id",
                        column: x => x.destination_branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_insurance_part_lines_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_insurance_part_lines_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "work_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body_panel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    cost_polish = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    cost_pdr = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    cost_remove_refit = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    cost_replace = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    cost_disassemble_assemble = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    cost_repair = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    cost_paint = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    cost_repair_paint = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    cost_weld = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    cost_other = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    discount_pct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_items_assessments_assessment_id",
                        column: x => x.assessment_id,
                        principalTable: "assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_work_items_body_panels_body_panel_id",
                        column: x => x.body_panel_id,
                        principalTable: "body_panels",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    repair_id = table.Column<Guid>(type: "uuid", nullable: true),
                    retail_repair_id = table.Column<Guid>(type: "uuid", nullable: true),
                    phase = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    file_path = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    caption = table.Column<string>(type: "text", nullable: true),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_photos_assessments_assessment_id",
                        column: x => x.assessment_id,
                        principalTable: "assessments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_photos_repairs_repair_id",
                        column: x => x.repair_id,
                        principalTable: "repairs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_photos_retail_repairs_retail_repair_id",
                        column: x => x.retail_repair_id,
                        principalTable: "retail_repairs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_photos_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_adjusters_insurance_company_id",
                table: "adjusters",
                column: "insurance_company_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_role_claims_role_id",
                table: "AspNetRoleClaims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_claims_user_id",
                table: "AspNetUserClaims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_logins_user_id",
                table: "AspNetUserLogins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_roles_role_id",
                table: "AspNetUserRoles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_branch_id",
                table: "AspNetUsers",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_customer_id",
                table: "AspNetUsers",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_insurance_company_id",
                table: "AspNetUsers",
                column: "insurance_company_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_supplier_id",
                table: "AspNetUsers",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assessments_insurance_case_id",
                table: "assessments",
                column: "insurance_case_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assessors_insurance_company_id",
                table: "assessors",
                column: "insurance_company_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_body_panels_code",
                table: "body_panels",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_branches_code",
                table: "branches",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_case_events_insurance_case_id",
                table: "case_events",
                column: "insurance_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_events_retail_case_id",
                table: "case_events",
                column: "retail_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_events_triggered_by_id",
                table: "case_events",
                column: "triggered_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_mobile_phone",
                table: "customers",
                column: "mobile_phone");

            migrationBuilder.CreateIndex(
                name: "ix_customers_vat_number",
                table: "customers",
                column: "vat_number");

            migrationBuilder.CreateIndex(
                name: "ix_documents_customer_id",
                table: "documents",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_insurance_case_id",
                table: "documents",
                column: "insurance_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_retail_case_id",
                table: "documents",
                column: "retail_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_uploaded_by_id",
                table: "documents",
                column: "uploaded_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_vehicle_id",
                table: "documents",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ix_insurance_approvals_insurance_case_id",
                table: "insurance_approvals",
                column: "insurance_case_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_insurance_approvals_insurance_company_id",
                table: "insurance_approvals",
                column: "insurance_company_id");

            migrationBuilder.CreateIndex(
                name: "ix_insurance_cases_adjuster_id",
                table: "insurance_cases",
                column: "adjuster_id");

            migrationBuilder.CreateIndex(
                name: "ix_insurance_cases_assessor_id",
                table: "insurance_cases",
                column: "assessor_id");

            migrationBuilder.CreateIndex(
                name: "ix_insurance_cases_assigned_user_id",
                table: "insurance_cases",
                column: "assigned_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_insurance_cases_branch_id_status",
                table: "insurance_cases",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_insurance_cases_case_number",
                table: "insurance_cases",
                column: "case_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_insurance_cases_customer_id",
                table: "insurance_cases",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_insurance_cases_insurance_company_id",
                table: "insurance_cases",
                column: "insurance_company_id");

            migrationBuilder.CreateIndex(
                name: "ix_insurance_cases_vehicle_id",
                table: "insurance_cases",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ix_insurance_companies_name",
                table: "insurance_companies",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_insurance_part_lines_assessment_id",
                table: "insurance_part_lines",
                column: "assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_insurance_part_lines_destination_branch_id",
                table: "insurance_part_lines",
                column: "destination_branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_insurance_part_lines_supplier_id",
                table: "insurance_part_lines",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_insurance_part_lines_warehouse_id",
                table: "insurance_part_lines",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_insurance_case_id",
                table: "payments",
                column: "insurance_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_retail_case_id",
                table: "payments",
                column: "retail_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_photos_assessment_id",
                table: "photos",
                column: "assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_photos_repair_id",
                table: "photos",
                column: "repair_id");

            migrationBuilder.CreateIndex(
                name: "ix_photos_retail_repair_id",
                table: "photos",
                column: "retail_repair_id");

            migrationBuilder.CreateIndex(
                name: "ix_photos_uploaded_by_id",
                table: "photos",
                column: "uploaded_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_insurance_case_id",
                table: "quotes",
                column: "insurance_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_quote_number",
                table: "quotes",
                column: "quote_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quotes_responsible_user_id",
                table: "quotes",
                column: "responsible_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_repairs_insurance_case_id",
                table: "repairs",
                column: "insurance_case_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_repairs_technician_id",
                table: "repairs",
                column: "technician_id");

            migrationBuilder.CreateIndex(
                name: "ix_retail_cases_assigned_user_id",
                table: "retail_cases",
                column: "assigned_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_retail_cases_branch_id_status",
                table: "retail_cases",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_retail_cases_case_number",
                table: "retail_cases",
                column: "case_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_retail_cases_customer_id",
                table: "retail_cases",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_retail_cases_vehicle_id",
                table: "retail_cases",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ix_retail_part_lines_destination_branch_id",
                table: "retail_part_lines",
                column: "destination_branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_retail_part_lines_retail_case_id",
                table: "retail_part_lines",
                column: "retail_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_retail_part_lines_supplier_id",
                table: "retail_part_lines",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_retail_part_lines_warehouse_id",
                table: "retail_part_lines",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_retail_repairs_retail_case_id",
                table: "retail_repairs",
                column: "retail_case_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_retail_repairs_technician_id",
                table: "retail_repairs",
                column: "technician_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_catalogs_code",
                table: "service_catalogs",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_customer_id",
                table: "vehicles",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_insurance_company_id",
                table: "vehicles",
                column: "insurance_company_id");

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_plate_number",
                table: "vehicles",
                column: "plate_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_vin",
                table: "vehicles",
                column: "vin");

            migrationBuilder.CreateIndex(
                name: "ix_warehouses_branch_id",
                table: "warehouses",
                column: "branch_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_items_assessment_id",
                table: "work_items",
                column: "assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_body_panel_id",
                table: "work_items",
                column: "body_panel_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "body_panel_operations");

            migrationBuilder.DropTable(
                name: "case_events");

            migrationBuilder.DropTable(
                name: "company_profiles");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "insurance_approvals");

            migrationBuilder.DropTable(
                name: "insurance_part_lines");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "photos");

            migrationBuilder.DropTable(
                name: "quotes");

            migrationBuilder.DropTable(
                name: "retail_part_lines");

            migrationBuilder.DropTable(
                name: "service_catalogs");

            migrationBuilder.DropTable(
                name: "work_items");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "repairs");

            migrationBuilder.DropTable(
                name: "retail_repairs");

            migrationBuilder.DropTable(
                name: "warehouses");

            migrationBuilder.DropTable(
                name: "assessments");

            migrationBuilder.DropTable(
                name: "body_panels");

            migrationBuilder.DropTable(
                name: "retail_cases");

            migrationBuilder.DropTable(
                name: "insurance_cases");

            migrationBuilder.DropTable(
                name: "adjusters");

            migrationBuilder.DropTable(
                name: "assessors");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "insurance_companies");
        }
    }
}
