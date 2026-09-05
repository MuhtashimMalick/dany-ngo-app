using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Replaces <c>application_categories.is_zakat_eligible</c> (boolean) with
    /// <c>fund_eligibility</c> (<c>ZakatOnly</c>/<c>GeneralOnly</c>/<c>Either</c>) — the two-state
    /// column couldn't express the client's confirmed final rule (v1.5 amendment, see
    /// docs/scope.md): Shaadi/Health/Education/House Rent/Emergency must draw from Zakat, Rozgar
    /// stays General-only, and Other becomes the sole dual-eligible category. Hand-edited after
    /// scaffolding: EF's own diff only knows how to `UpdateData` the 7 seeded rows by id, which
    /// would silently leave any admin-created category's new column at an invalid default. The
    /// backfill here instead runs as two SQL passes over every row (see below), and the
    /// `fn_enforce_zakat_eligibility` trigger function is updated in place — the trigger itself
    /// (`trg_applications_enforce_zakat_eligibility`, from
    /// 20260731081350_AddZakatTriggerAndFundBalanceView) is untouched.
    /// </summary>
    public partial class AddApplicationCategoryFundEligibility : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nullable at first: the two backfill passes below populate every existing row
            // (seeded or admin-created) before NOT NULL is enforced.
            migrationBuilder.AddColumn<string>(
                name: "fund_eligibility",
                table: "application_categories",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Pass A — faithful translation of the old boolean, covers every row including a
            // hypothetical admin-created category the client's ruling below never mentions:
            // true (Zakat-eligible) -> Either, false -> GeneralOnly (its only legal value before
            // today, since only the two-state rule existed).
            migrationBuilder.Sql("""
                UPDATE application_categories
                SET fund_eligibility = CASE WHEN is_zakat_eligible THEN 'Either' ELSE 'GeneralOnly' END;
                """);

            // Pass B — the client's confirmed override for the 7 seeded categories, applied after
            // Pass A so it only tightens/loosens the categories the client actually ruled on; any
            // custom category created via the admin API keeps the Pass A behavior unchanged.
            migrationBuilder.Sql("""
                UPDATE application_categories
                SET fund_eligibility = 'ZakatOnly'
                WHERE code IN ('SHAADI', 'HEALTH', 'EDUCATION', 'HOUSE_RENT', 'EMERGENCY');
                """);
            migrationBuilder.Sql("""
                UPDATE application_categories SET fund_eligibility = 'GeneralOnly' WHERE code = 'ROZGAR';
                """);
            migrationBuilder.Sql("""
                UPDATE application_categories SET fund_eligibility = 'Either' WHERE code = 'OTHER';
                """);

            // Safety guard — the new rule is stricter than the old one for ZakatOnly/GeneralOnly
            // (Either used to silently accept both funds). If any pre-existing `applications` row
            // was posted against a fund that the newly-assigned fund_eligibility now forbids, that
            // row would become permanently uneditable through the API afterward
            // (FundApplicationService.UpdateAsync calls EnsureFundIsCompatible on every update, and
            // an Active loan agreement even blocks changing the fund to fix it). Fail the migration
            // loudly instead of applying it over data that can no longer be corrected in-app.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    v_violations integer;
                BEGIN
                    SELECT COUNT(*) INTO v_violations
                    FROM applications a
                    JOIN application_categories ac ON ac.id = a.application_category_id
                    JOIN fund_categories fc ON fc.id = a.fund_category_id
                    WHERE (ac.fund_eligibility = 'ZakatOnly' AND NOT fc.is_zakat)
                       OR (ac.fund_eligibility = 'GeneralOnly' AND fc.is_zakat);

                    IF v_violations > 0 THEN
                        RAISE EXCEPTION
                            'AddApplicationCategoryFundEligibility: % existing application(s) would violate the new fund_eligibility rule. Remediate before re-running this migration; detect with (run against the pre-migration schema, before fund_eligibility exists): SELECT a.id, a.application_number, ac.code AS category, fc.code AS fund FROM applications a JOIN application_categories ac ON ac.id = a.application_category_id JOIN fund_categories fc ON fc.id = a.fund_category_id WHERE (ac.code IN (''SHAADI'',''HEALTH'',''EDUCATION'',''HOUSE_RENT'',''EMERGENCY'') AND NOT fc.is_zakat) OR (ac.code = ''ROZGAR'' AND fc.is_zakat);',
                            v_violations;
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "fund_eligibility",
                table: "application_categories",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "is_zakat_eligible",
                table: "application_categories");

            migrationBuilder.AddCheckConstraint(
                name: "ck_application_categories_fund_eligibility",
                table: "application_categories",
                sql: "fund_eligibility IN ('ZakatOnly','GeneralOnly','Either')");

            // Three-way predicate replaces the old true/false check. Function body only — the
            // trigger that calls it is untouched.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_enforce_zakat_eligibility() RETURNS trigger AS $$
                DECLARE
                    v_is_zakat boolean;
                    v_fund_eligibility varchar(20);
                BEGIN
                    SELECT is_zakat INTO v_is_zakat
                    FROM fund_categories WHERE id = NEW.fund_category_id;

                    SELECT fund_eligibility INTO v_fund_eligibility
                    FROM application_categories WHERE id = NEW.application_category_id;

                    IF v_fund_eligibility = 'ZakatOnly' AND NOT v_is_zakat THEN
                        RAISE EXCEPTION
                            'Application category % requires a Zakat fund and cannot be funded from non-Zakat fund %',
                            NEW.application_category_id, NEW.fund_category_id
                            USING ERRCODE = 'check_violation';
                    END IF;

                    IF v_fund_eligibility = 'GeneralOnly' AND v_is_zakat THEN
                        RAISE EXCEPTION
                            'Application category % is not Zakat-eligible and cannot be funded from Zakat fund %',
                            NEW.application_category_id, NEW.fund_category_id
                            USING ERRCODE = 'check_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_application_categories_fund_eligibility",
                table: "application_categories");

            migrationBuilder.AddColumn<bool>(
                name: "is_zakat_eligible",
                table: "application_categories",
                type: "boolean",
                nullable: true);

            // Lossy for Either (both ZakatOnly and Either round-trip to true) — inherent to the
            // old two-state encoding, same as the client's original ruling before this amendment.
            migrationBuilder.Sql("""
                UPDATE application_categories
                SET is_zakat_eligible = (fund_eligibility IN ('ZakatOnly', 'Either'));
                """);

            migrationBuilder.AlterColumn<bool>(
                name: "is_zakat_eligible",
                table: "application_categories",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "fund_eligibility",
                table: "application_categories");

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_enforce_zakat_eligibility() RETURNS trigger AS $$
                DECLARE
                    v_is_zakat boolean;
                    v_is_zakat_eligible boolean;
                BEGIN
                    SELECT is_zakat INTO v_is_zakat
                    FROM fund_categories WHERE id = NEW.fund_category_id;

                    SELECT is_zakat_eligible INTO v_is_zakat_eligible
                    FROM application_categories WHERE id = NEW.application_category_id;

                    IF v_is_zakat AND NOT v_is_zakat_eligible THEN
                        RAISE EXCEPTION
                            'Application category % is not Zakat-eligible and cannot be funded from Zakat fund %',
                            NEW.application_category_id, NEW.fund_category_id
                            USING ERRCODE = 'check_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);
        }
    }
}
