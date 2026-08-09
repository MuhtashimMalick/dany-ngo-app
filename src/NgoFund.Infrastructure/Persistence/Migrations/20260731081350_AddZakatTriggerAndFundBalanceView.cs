using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Hand-written SQL that the EF Core fluent API cannot express: the Zakat-eligibility
    /// trigger (defence in depth alongside the domain-layer check in
    /// <c>FundApplication.EnsureFundIsCompatible</c>) and the <c>vw_fund_balances</c> view the
    /// dashboard reads instead of any stored balance column.
    /// </summary>
    public partial class AddZakatTriggerAndFundBalanceView : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_applications_enforce_zakat_eligibility
                BEFORE INSERT OR UPDATE OF fund_category_id, application_category_id ON applications
                FOR EACH ROW EXECUTE FUNCTION fn_enforce_zakat_eligibility();
                """);

            migrationBuilder.Sql("""
                CREATE VIEW vw_fund_balances AS
                SELECT
                    fc.id AS fund_category_id,
                    fc.code,
                    fc.name,
                    fc.is_zakat,
                    COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Credit'), 0) AS total_collected,
                    COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Debit'), 0) AS total_utilized,
                    COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Credit'), 0)
                        - COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Debit'), 0) AS balance
                FROM fund_categories fc
                LEFT JOIN fund_transactions ft ON ft.fund_category_id = fc.id
                GROUP BY fc.id, fc.code, fc.name, fc.is_zakat;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_fund_balances;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_applications_enforce_zakat_eligibility ON applications;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_enforce_zakat_eligibility();");
        }
    }
}
