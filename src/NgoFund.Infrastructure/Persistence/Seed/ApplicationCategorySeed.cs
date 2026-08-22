using Microsoft.EntityFrameworkCore;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Seed;

/// <summary>
/// The application categories from the client scope document. "- Z" marked categories are
/// Zakat-eligible; Rozgar/Business Help and Other are not, per the client's explicit ruling on
/// the Zakat rule.
/// </summary>
internal static class ApplicationCategorySeed
{
    public static readonly Guid ShaadiId = DeterministicGuid.From("application-category:SHAADI");
    public static readonly Guid HealthId = DeterministicGuid.From("application-category:HEALTH");
    public static readonly Guid EducationId = DeterministicGuid.From("application-category:EDUCATION");
    public static readonly Guid HouseRentId = DeterministicGuid.From("application-category:HOUSE_RENT");
    public static readonly Guid EmergencyId = DeterministicGuid.From("application-category:EMERGENCY");
    public static readonly Guid RozgarId = DeterministicGuid.From("application-category:ROZGAR");
    public static readonly Guid OtherId = DeterministicGuid.From("application-category:OTHER");

    private const string TermsVersion = "1.0";

    // Raw string literals preserve the source file's literal line endings, which vary by checkout
    // (core.autocrlf, no .gitattributes pin) — ReplaceLineEndings normalizes to LF so the seeded
    // value is byte-stable regardless of OS/git config, matching what's baked into the migration.
    private static readonly string HousingTermsText = """
        مکان مدد کے قوانین و ضوابط

        1. مکان مدد کی درخواست کی ادائیگی زکوٰۃ فنڈ سے کی جائے گی۔
        Payment for housing assistance will be made from the Zakat Fund.

        2. درخواست گزار کا جماعت کا فعال ممبر ہونا ضروری ہے۔
        The applicant must be an active Jamaat member.

        3. درخواست کے ساتھ قومی شناختی کارڈ اور جماعت کے ممبر شپ کارڈ کی کاپیاں منسلک کرنا لازمی ہے۔
        Copies of CNIC and Jamaat membership card must be attached with the application.

        4. عورت اپنے شوہر کی موجودگی میں اپنے شوہر کے نام سے درخواست جمع کروانے کی پابند ہو گی۔
        A woman must submit the application in her husband's name and in his presence.

        5. درخواست کے ساتھ گزشتہ تین ماہ کے بلوں (بجلی، گیس، فون، پانی) اور کرایہ کی رسیدوں کی فوٹو کاپیاں منسلک کرنا لازمی ہے۔
        Photocopies of the last three months' utility bills (electricity, gas, phone, water) and rent receipts must be attached.

        6. مکان مدد کمیٹی جب بھی درخواست گزار کو بلائے، اسے حاضر ہو کر تمام تفصیلات سے آگاہ کرنا ہو گا؛ ضرورت پڑنے پر موجودہ رہائش کا معائنہ اور جانچ پڑتال کروانے کا بھی پابند ہو گا۔
        Whenever called by the Housing Assistance Committee, the applicant must appear and provide full details, and must allow inspection of the current residence if required.

        7. مکان کا کرایہ، بجلی، گیس، پانی، ٹیکس اور دیگر اخراجات وقت پر ادا کرنے کا پابند ہو گا۔
        The applicant must pay house rent, electricity, gas, water, tax and other expenses on time.

        8. درخواست منظور یا مسترد کرنے کا مکمل اختیار مجلسِ عامہ کے پاس ہے، درخواست گزار کسی قسم کی مداخلت نہیں کرے گا۔
        Full authority to approve or reject the application rests with the General Council; the applicant will not interfere in any way.

        9. نامکمل یا غلط بیانی کی صورت میں درخواست فارم مسترد ہو جائے گا۔
        An incomplete application or false statement will result in rejection of the form.
        """.ReplaceLineEndings("\n");

    /// <summary>Item 2 was reconstructed from a heavily garbled OCR section of the source document
    /// per the client — worth double-checking against the original PDF before treating it as
    /// final, in case the exact wording differs.</summary>
    private static readonly string MarriageTermsText = """
        اہم ہدایات

        1. درخواست کے ساتھ جماعت کے ممبر شپ کارڈ کی فوٹو کاپی اور اصل شادی کارڈ منسلک کرنا ضروری ہے۔
        A photocopy of the Jamaat membership card and the original marriage card must be attached with the application.

        2. درخواست فارم صرف متعلقہ دولہا/دلہن کے نام سے وصول کیا جائے گا۔
        The application form will only be accepted in the name of the concerned bride/groom.

        3. کمیٹی کو کسی بھی درخواست کی تفصیلی انکوائری کا حق حاصل ہے۔
        The committee reserves the right to conduct a detailed inquiry into any application.

        4. فارم کے تمام اندراجات پُر کرنا ضروری ہے، نامکمل فارم مسترد کر دیا جائے گا۔
        All entries in the form must be completed; an incomplete form will be rejected.

        5. غلط معلومات فراہم کرنے کی صورت میں فارم مسترد کر دیا جائے گا۔
        The form will be rejected if incorrect information is provided.

        6. دلہن کے شناختی کارڈ یا فارم "ب" کی کاپی لازمی طور پر منسلک کریں، ورنہ فارم مسترد کر دیا جائے گا۔
        A copy of the bride's CNIC or Form-B must be attached, otherwise the form will be rejected.

        7. درخواست فارم شادی سے کم از کم ایک ماہ قبل جمع کروانا لازمی ہے، جس کے ساتھ اصل شادی کارڈ، شناختی کارڈ (دولہا/دلہن) کی کاپی، اور جماعت کی ممبر شپ کی کاپی منسلک کریں۔
        The application must be submitted at least one month before the wedding, together with the original marriage card, a copy of CNIC (groom/bride), and a copy of the Jamaat membership card.
        """.ReplaceLineEndings("\n");

    private static readonly string BusinessLoanTermsText = """
        شرائط و ضوابط

        1. آپ اپنے کاروبار کی تفصیل بتا کر ٹرسٹ سے قابلِ واپسی قرض لے سکتے ہیں۔ درخواست صحیح اور مکمل بھر کر ضروری دستاویزات کے ساتھ جمع کروائیں؛ نامکمل درخواست منظور نہیں کی جائے گی۔
        You may apply for a repayable loan from the Trust by describing your business. The application must be complete and accurate, with required documents attached; incomplete applications will not be approved.

        2. قرض کی وصولی کی تاریخ سے ماہوار اقساط پیشگی چیک کے ذریعے وصول کی جائیں گی، اس لیے درخواست گزار کا بینک اکاؤنٹ ہونا لازمی ہے۔
        Monthly installments will be collected via post-dated cheque from the date of loan disbursement, so the applicant must have a bank account.

        3. درخواست گزار ہر تین ماہ بعد اپنے کاروبار کی تفصیلی رپورٹ خود کمیٹی کے روبرو پیش کرے گا۔
        The applicant must present a detailed report of their business in person before the committee every three months.

        4. کاروبار کی ناکامی یا دھوکہ دہی کی صورت میں کمیٹی باقی رقم یکمشت وصول کرنے کا فیصلہ کر سکتی ہے۔
        In case of business failure or fraud, the committee may decide to recover the remaining amount in a lump sum.

        5. اگر درخواست گزار نے بینک یا کسی اور ادارے سے قرض لیا ہے تو اس کی تفصیل درخواست میں درج کرنا لازمی ہے۔
        If the applicant has an existing loan from a bank or any other institution, its details must be disclosed in the application.

        6. درخواست ایک مرتبہ مسترد ہونے کی صورت میں چھ ماہ تک دوبارہ جمع نہیں کروائی جا سکے گی۔
        A rejected application cannot be resubmitted for six months.

        7. دونوں ضامن افراد کا اے-زی ڈینی ویلفیئر ٹرسٹ کا ممبر اور کاروباری شخصیت ہونا لازمی ہے۔ درخواست گزار کسی وجہ سے ادائیگی میں ناکام ہو تو دونوں ضامن باقی رقم روزگار اسکیم کمیٹی کو واپس ادا کریں گے۔
        Both guarantors must be A.Z Dany Welfare Trust members and business persons. If the applicant defaults for any reason, both guarantors will repay the remaining amount to the Rozgar Scheme Committee.

        8. تمام عہدے داران، مبلغ، ورکنگ کمیٹی اور نامزد اراکین درخواست فارم پر ضامن نہیں بن سکتے۔
        All office bearers, missionaries, working committee members, and nominated members cannot act as guarantors.

        9. روزگار اسکیم کمیٹی کو یہ اختیار حاصل ہے کہ وہ کوئی وجہ بتائے بغیر کسی بھی درخواست کو منظور یا مسترد کر دے؛ اس کا فیصلہ حتمی اور آخری ہو گا۔
        The Rozgar Scheme Committee has the authority to approve or reject any application without stating a reason; its decision is final.
        """.ReplaceLineEndings("\n");

    public static void Apply(ModelBuilder builder)
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // RequiresGuarantors is data-driven: only ROZGAR (Business Loan) gates Approved on
        // guarantor count, per the client's ruling — every other category is 0 (no gate).
        (Guid Id, string Code, string Name, bool IsZakatEligible, int Order, int RequiresGuarantors, string? TermsText, string? TermsVersion)[] rows =
        [
            (ShaadiId, "SHAADI", "Shaadi Fund Request", true, 1, 0, MarriageTermsText, TermsVersion),
            (HealthId, "HEALTH", "Health Fund Request", true, 2, 0, null, null),
            (EducationId, "EDUCATION", "Education Support", true, 3, 0, null, null),
            (HouseRentId, "HOUSE_RENT", "House Rent/Help", true, 4, 0, HousingTermsText, TermsVersion),
            (EmergencyId, "EMERGENCY", "Emergency Support", true, 5, 0, null, null),
            (RozgarId, "ROZGAR", "Rozgar/Business Help", false, 6, 2, BusinessLoanTermsText, TermsVersion),
            (OtherId, "OTHER", "Other", false, 7, 0, null, null),
        ];

        builder.Entity<ApplicationCategory>().HasData(rows.Select(r => new ApplicationCategory
        {
            Id = r.Id,
            Code = r.Code,
            Name = r.Name,
            IsZakatEligible = r.IsZakatEligible,
            IsActive = true,
            DisplayOrder = r.Order,
            RequiresGuarantors = r.RequiresGuarantors,
            TermsText = r.TermsText,
            TermsVersion = r.TermsVersion,
            CreatedAt = now,
        }));
    }
}
