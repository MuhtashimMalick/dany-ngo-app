using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Hand-written SQL (the EF Core fluent API cannot express triggers or views) for the M7 loan
    /// (qard al-hasan) feature: five triggers mirroring the existing
    /// <c>fn_enforce_zakat_eligibility</c>/<c>fn_enforce_payment_approved_amount_cap</c> defence-in-depth
    /// pattern (D7), a rewrite of <c>vw_fund_balances</c> to account for loan repayments without
    /// treating them as donations (D4), and a new <c>vw_loan_balances</c> view.
    /// </summary>
    public partial class AddLoanTriggersAndViews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D7#1 — a loan agreement's fund can never be a Zakat fund. Domain-layer twin:
            // LoanAgreement.EnsureFundIsRepayable.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_enforce_loan_agreement_fund_eligibility() RETURNS trigger AS $$
                DECLARE
                    v_is_zakat boolean;
                BEGIN
                    SELECT is_zakat INTO v_is_zakat FROM fund_categories WHERE id = NEW.fund_category_id;

                    IF v_is_zakat THEN
                        RAISE EXCEPTION
                            'Loan agreement % cannot be created or moved against a Zakat fund (fund_category_id %)',
                            NEW.id, NEW.fund_category_id
                            USING ERRCODE = 'check_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_loan_agreements_enforce_fund_eligibility
                BEFORE INSERT OR UPDATE OF fund_category_id ON loan_agreements
                FOR EACH ROW EXECUTE FUNCTION fn_enforce_loan_agreement_fund_eligibility();
                """);

            // D7#2 — a repayment against a Zakat-fund loan agreement can never exist, checked via
            // the agreement's (frozen) fund.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_enforce_loan_repayment_fund_eligibility() RETURNS trigger AS $$
                DECLARE
                    v_is_zakat boolean;
                BEGIN
                    SELECT fc.is_zakat INTO v_is_zakat
                    FROM loan_agreements la
                    JOIN fund_categories fc ON fc.id = la.fund_category_id
                    WHERE la.id = NEW.loan_agreement_id;

                    IF v_is_zakat THEN
                        RAISE EXCEPTION
                            'Loan repayment % cannot be recorded against Zakat-fund loan agreement %',
                            NEW.id, NEW.loan_agreement_id
                            USING ERRCODE = 'check_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_loan_repayments_enforce_fund_eligibility
                BEFORE INSERT OR UPDATE OF loan_agreement_id ON loan_repayments
                FOR EACH ROW EXECUTE FUNCTION fn_enforce_loan_repayment_fund_eligibility();
                """);

            // D7#3 — mirrors fn_enforce_payment_approved_amount_cap: total completed repayments for
            // a loan agreement can never exceed its disbursed principal (SUM of completed payments
            // against the application, restricted to the agreement's own fund per D1).
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_enforce_loan_repayment_cap() RETURNS trigger AS $$
                DECLARE
                    v_application_id uuid;
                    v_fund_category_id uuid;
                    v_principal_disbursed numeric(18,2);
                    v_total_completed numeric(18,2);
                BEGIN
                    -- Voided repayments don't draw against the cap.
                    IF NEW.status <> 'Completed' THEN
                        RETURN NEW;
                    END IF;

                    SELECT application_id, fund_category_id INTO v_application_id, v_fund_category_id
                    FROM loan_agreements WHERE id = NEW.loan_agreement_id;

                    SELECT COALESCE(SUM(amount), 0) INTO v_principal_disbursed
                    FROM payments
                    WHERE application_id = v_application_id
                      AND fund_category_id = v_fund_category_id
                      AND status = 'Completed';

                    SELECT COALESCE(SUM(amount), 0) INTO v_total_completed
                    FROM loan_repayments
                    WHERE loan_agreement_id = NEW.loan_agreement_id
                      AND status = 'Completed'
                      AND id <> NEW.id;

                    v_total_completed := v_total_completed + NEW.amount;

                    IF v_total_completed > v_principal_disbursed THEN
                        RAISE EXCEPTION
                            'Repayment of % would bring total completed repayments for loan agreement % to %, exceeding disbursed principal of %',
                            NEW.amount, NEW.loan_agreement_id, v_total_completed, v_principal_disbursed
                            USING ERRCODE = 'check_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_loan_repayments_enforce_cap
                BEFORE INSERT OR UPDATE OF amount, status, loan_agreement_id ON loan_repayments
                FOR EACH ROW EXECUTE FUNCTION fn_enforce_loan_repayment_cap();
                """);

            // D7#4 / D5 — a deferrable constraint trigger: an installment schedule must always sum
            // to exactly its agreement's principal_amount (the "no interest" guarantee, checked at
            // the database level too, not just LoanScheduleCalculator). Deferred to end of
            // transaction since a schedule is inserted row-by-row.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_enforce_installment_schedule_totals() RETURNS trigger AS $$
                DECLARE
                    v_loan_agreement_id uuid;
                    v_principal_amount numeric(18,2);
                    v_total_due numeric(18,2);
                BEGIN
                    v_loan_agreement_id := COALESCE(NEW.loan_agreement_id, OLD.loan_agreement_id);

                    SELECT principal_amount INTO v_principal_amount
                    FROM loan_agreements WHERE id = v_loan_agreement_id;

                    SELECT COALESCE(SUM(amount_due), 0) INTO v_total_due
                    FROM loan_installments WHERE loan_agreement_id = v_loan_agreement_id;

                    IF v_total_due <> v_principal_amount THEN
                        RAISE EXCEPTION
                            'Installment schedule for loan agreement % sums to %, which does not equal its principal amount of %',
                            v_loan_agreement_id, v_total_due, v_principal_amount
                            USING ERRCODE = 'check_violation';
                    END IF;

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                CREATE CONSTRAINT TRIGGER trg_loan_installments_enforce_schedule_totals
                AFTER INSERT OR UPDATE OR DELETE ON loan_installments
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION fn_enforce_installment_schedule_totals();
                """);

            // D7#5 — unconditional: any payment drawing from a non-Zakat fund requires an Active
            // loan agreement for its application, no grandfathering/date-cutoff clause. Domain-layer
            // twin: PaymentService.CreateAsync's LoanPlanRequiredException gate.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_enforce_loan_plan_before_disbursement() RETURNS trigger AS $$
                DECLARE
                    v_is_zakat boolean;
                    v_has_active_agreement boolean;
                BEGIN
                    SELECT is_zakat INTO v_is_zakat FROM fund_categories WHERE id = NEW.fund_category_id;

                    IF v_is_zakat THEN
                        RETURN NEW;
                    END IF;

                    SELECT EXISTS (
                        SELECT 1 FROM loan_agreements
                        WHERE application_id = NEW.application_id AND status = 'Active'
                    ) INTO v_has_active_agreement;

                    IF NOT v_has_active_agreement THEN
                        RAISE EXCEPTION
                            'Application % draws from a non-Zakat fund and requires an active loan agreement before it can accept payments',
                            NEW.application_id
                            USING ERRCODE = 'check_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_payments_enforce_loan_plan_before_disbursement
                BEFORE INSERT ON payments
                FOR EACH ROW EXECUTE FUNCTION fn_enforce_loan_plan_before_disbursement();
                """);

            // D4 — vw_fund_balances rewrite: a loan repayment is a Credit but NOT a donation (must
            // never inflate total_collected), and a loan repayment reversal is a Debit but NOT a
            // payment (must never inflate total_utilized). New total_repaid column. balance stays
            // the exhaustive, reference-type-blind SUM(Credit) - SUM(Debit) — repaid money is
            // disbursable again automatically.
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_fund_balances;");

            // "LoanRepaymentReversal" (21 chars) doesn't fit the original varchar(20) reference_type
            // column — widened to 30 for headroom. Must happen while no view depends on the column
            // (Postgres refuses ALTER COLUMN TYPE on a column a view references), which is exactly
            // why this sits between the DROP VIEW above and the CREATE VIEW below rather than in
            // its own migration.
            migrationBuilder.AlterColumn<string>(
                name: "reference_type",
                table: "fund_transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.Sql("""
                CREATE VIEW vw_fund_balances AS
                SELECT
                    fc.id AS fund_category_id,
                    fc.code,
                    fc.name,
                    fc.is_zakat,
                    (COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Credit' AND ft.reference_type NOT IN ('PaymentReversal', 'LoanRepayment')), 0)
                     - COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Debit' AND ft.reference_type = 'DonationReversal'), 0)
                    )::numeric(18,2) AS total_collected,
                    (COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Debit' AND ft.reference_type NOT IN ('DonationReversal', 'LoanRepaymentReversal')), 0)
                     - COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Credit' AND ft.reference_type = 'PaymentReversal'), 0)
                    )::numeric(18,2) AS total_utilized,
                    (COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Credit' AND ft.reference_type = 'LoanRepayment'), 0)
                     - COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Debit' AND ft.reference_type = 'LoanRepaymentReversal'), 0)
                    )::numeric(18,2) AS total_repaid,
                    (COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Credit'), 0)
                     - COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Debit'), 0)
                    )::numeric(18,2) AS balance
                FROM fund_categories fc
                LEFT JOIN fund_transactions ft ON ft.fund_category_id = fc.id
                GROUP BY fc.id, fc.code, fc.name, fc.is_zakat;
                """);

            // vw_loan_balances — derives every loan figure from the ledger/payments/repayments the
            // same way vw_fund_balances derives fund balances, never a stored column.
            // overdue_amount is informational only (see LoanScheduleCalculator.Allocate for the
            // authoritative per-installment figure) and is NEVER folded into outstanding_balance,
            // which always stays exactly principal_disbursed - total_repaid regardless of lateness.
            migrationBuilder.Sql("""
                CREATE VIEW vw_loan_balances AS
                SELECT
                    la.id AS loan_agreement_id,
                    la.application_id,
                    la.fund_category_id,
                    la.principal_amount AS principal_scheduled,
                    COALESCE(disb.total, 0)::numeric(18,2) AS principal_disbursed,
                    COALESCE(rep.total, 0)::numeric(18,2) AS total_repaid,
                    (COALESCE(disb.total, 0) - COALESCE(rep.total, 0))::numeric(18,2) AS outstanding_balance,
                    nxt.next_due_date,
                    GREATEST(0, LEAST(COALESCE(overdue.total, 0), COALESCE(disb.total, 0)) - COALESCE(rep.total, 0))::numeric(18,2) AS overdue_amount
                FROM loan_agreements la
                LEFT JOIN LATERAL (
                    SELECT SUM(p.amount) AS total
                    FROM payments p
                    WHERE p.application_id = la.application_id
                      AND p.fund_category_id = la.fund_category_id
                      AND p.status = 'Completed'
                ) disb ON true
                LEFT JOIN LATERAL (
                    SELECT SUM(r.amount) AS total
                    FROM loan_repayments r
                    WHERE r.loan_agreement_id = la.id
                      AND r.status = 'Completed'
                ) rep ON true
                LEFT JOIN LATERAL (
                    SELECT MIN(i.due_date) AS next_due_date
                    FROM (
                        SELECT i2.due_date, SUM(i2.amount_due) OVER (ORDER BY i2.sequence_no) AS cumulative_due
                        FROM loan_installments i2
                        WHERE i2.loan_agreement_id = la.id
                    ) i
                    WHERE i.cumulative_due > COALESCE(rep.total, 0)
                ) nxt ON true
                LEFT JOIN LATERAL (
                    SELECT SUM(i3.amount_due) AS total
                    FROM loan_installments i3
                    WHERE i3.loan_agreement_id = la.id AND i3.due_date < CURRENT_DATE
                ) overdue ON true;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_loan_balances;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_fund_balances;");

            migrationBuilder.AlterColumn<string>(
                name: "reference_type",
                table: "fund_transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            // Restore the prior (FixFundBalanceViewReversalSemantics) definition, without total_repaid.
            migrationBuilder.Sql("""
                CREATE VIEW vw_fund_balances AS
                SELECT
                    fc.id AS fund_category_id,
                    fc.code,
                    fc.name,
                    fc.is_zakat,
                    (COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Credit' AND ft.reference_type <> 'PaymentReversal'), 0)
                     - COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Debit' AND ft.reference_type = 'DonationReversal'), 0)
                    )::numeric(18,2) AS total_collected,
                    (COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Debit' AND ft.reference_type <> 'DonationReversal'), 0)
                     - COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Credit' AND ft.reference_type = 'PaymentReversal'), 0)
                    )::numeric(18,2) AS total_utilized,
                    (COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Credit'), 0)
                     - COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Debit'), 0)
                    )::numeric(18,2) AS balance
                FROM fund_categories fc
                LEFT JOIN fund_transactions ft ON ft.fund_category_id = fc.id
                GROUP BY fc.id, fc.code, fc.name, fc.is_zakat;
                """);

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_payments_enforce_loan_plan_before_disbursement ON payments;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_enforce_loan_plan_before_disbursement();");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_loan_installments_enforce_schedule_totals ON loan_installments;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_enforce_installment_schedule_totals();");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_loan_repayments_enforce_cap ON loan_repayments;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_enforce_loan_repayment_cap();");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_loan_repayments_enforce_fund_eligibility ON loan_repayments;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_enforce_loan_repayment_fund_eligibility();");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_loan_agreements_enforce_fund_eligibility ON loan_agreements;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_enforce_loan_agreement_fund_eligibility();");
        }
    }
}
