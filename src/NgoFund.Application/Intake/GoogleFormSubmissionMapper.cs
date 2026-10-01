using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Intake;
using NgoFund.Domain.Applications;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;

namespace NgoFund.Application.Intake;

/// <summary>
/// C5: pure, EF-free mapping from one raw <see cref="GoogleFormSubmissionRequest"/> to a
/// <see cref="GoogleFormMappingResult"/> — no DB access, fully unit-testable. One private method
/// per application category (the six forms), dispatched by the "Application Type" answer.
/// <see cref="GoogleFormIntakeService"/> (Infrastructure) is the only caller and does all the
/// actual writing, reusing the existing applicant/application/details/document services.
/// </summary>
public static class GoogleFormSubmissionMapper
{
    private static readonly Dictionary<string, string> ApplicationTypeToCategoryCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Housing Assistance"] = "HOUSE_RENT",
        ["Marriage Assistance"] = "SHAADI",
        ["Business Loan"] = "ROZGAR",
        ["Education Assistance"] = "EDUCATION",
        ["Health Assistance"] = "HEALTH",
        ["General Assistance Form"] = "OTHER",
    };

    public static GoogleFormMappingResult Map(GoogleFormSubmissionRequest request)
    {
        // The live "Application Type" radio's own option values carry an Urdu parenthetical (e.g.
        // "Housing Assistance (رہائشی مکان مدد)") — stripped the same way any other radio/dropdown
        // option value is (see GoogleFormAnswerLookup.GetOption).
        if (!ApplicationTypeToCategoryCode.TryGetValue(FormValueParser.StripOptionParenthetical(request.ApplicationType), out var categoryCode))
        {
            throw new UnrecognizedGoogleFormDataException($"'{request.ApplicationType}' is not a recognized application type.");
        }

        var answers = new GoogleFormAnswerLookup(request.Answers);
        var notes = new List<string>();

        var result = categoryCode switch
        {
            "HOUSE_RENT" => MapHousing(answers, request, notes),
            "SHAADI" => MapMarriage(answers, request, notes),
            "ROZGAR" => MapBusinessLoan(answers, request, notes),
            "EDUCATION" => MapEducation(answers, request, notes),
            "HEALTH" => MapHealth(answers, request, notes),
            "OTHER" => MapOther(answers, request, notes),
            _ => throw new UnrecognizedGoogleFormDataException($"'{request.ApplicationType}' is not a recognized application type."),
        };

        // Every answer no per-category method read becomes a note — nothing the applicant typed is
        // silently lost, even if this mapper doesn't yet have a structured home for it.
        foreach (var unconsumed in answers.GetUnconsumed())
        {
            var value = string.Join("; ", unconsumed.Values);
            if (!string.IsNullOrWhiteSpace(value))
            {
                notes.Add($"Unmapped answer '{unconsumed.Title}': {value}");
            }
        }

        var files = MapFiles(categoryCode, request.Files, notes);

        return result with { Notes = [.. result.Notes, .. notes], Files = files };
    }

    // ---------- shared helpers ----------

    private static decimal? Money(GoogleFormAnswerLookup a, string title, List<string> notes)
    {
        var raw = a.GetString(title);
        var value = FormValueParser.ParseDecimalOrNull(raw);
        if (raw is not null && value is null) notes.Add($"Could not parse '{title}' value '{raw}' as an amount.");
        return value;
    }

    private static int? Int(GoogleFormAnswerLookup a, string title, List<string> notes)
    {
        var raw = a.GetString(title);
        var value = FormValueParser.ParseIntOrNull(raw);
        if (raw is not null && value is null) notes.Add($"Could not parse '{title}' value '{raw}' as a number.");
        return value;
    }

    /// <summary>The one mandatory/unique CNIC per submission (the applicant's own) — unparseable
    /// fails the whole mapping (C5), unlike every other CNIC on a form (bride/guarantor/mother),
    /// which degrades to null + a note via <see cref="OtherCnic"/>.</summary>
    private static string ApplicantCnic(GoogleFormAnswerLookup a, string title)
    {
        var raw = a.GetString(title);
        return FormValueParser.NormalizeCnicOrNull(raw)
            ?? throw new UnrecognizedGoogleFormDataException($"'{title}' ('{raw}') is not a valid 13-digit CNIC — the applicant's CNIC is mandatory.");
    }

    private static string? OtherCnic(GoogleFormAnswerLookup a, string title, List<string> notes)
    {
        var raw = a.GetString(title);
        var value = FormValueParser.NormalizeCnicOrNull(raw);
        if (raw is not null && value is null) notes.Add($"Could not parse '{title}' value '{raw}' as a CNIC.");
        return value;
    }

    private static string? Phone(GoogleFormAnswerLookup a, string title, List<string> notes)
    {
        var raw = a.GetString(title);
        var value = FormValueParser.NormalizePhoneOrNull(raw);
        if (raw is not null && value is null) notes.Add($"Could not parse '{title}' value '{raw}' as a mobile number.");
        return value;
    }

    private static MaritalStatus? MaritalStatusOption(GoogleFormAnswerLookup a, string title, List<string> notes)
    {
        var raw = a.GetOption(title);
        if (raw is null) return null;
        try
        {
            return MaritalStatusMapper.MapFormLabel(raw);
        }
        catch (ArgumentException)
        {
            notes.Add($"Could not map '{title}' value '{raw}' to a marital status.");
            return null;
        }
    }

    /// <summary>The "House Status" radio, shared by Housing and Business Loan — both forms ask the
    /// same question with (near-)identical options.</summary>
    private static HouseStatus? HouseStatusOption(GoogleFormAnswerLookup a, string title, List<string> notes)
    {
        var raw = a.GetOption(title);
        if (raw is null) return null;
        if (Enum.TryParse<HouseStatus>(raw, ignoreCase: true, out var value)) return value;
        notes.Add($"Could not map '{title}' value '{raw}' to a house status.");
        return null;
    }

    /// <summary>E: a mapped string bound for a length-capped column. Over-length becomes null + a
    /// note carrying the FULL original value (never truncated — that would corrupt an identifier
    /// like a membership number) rather than letting Postgres reject the insert with 22001, which
    /// Apps Script would then retry forever.</summary>
    private static string? Bounded(string? value, int maxLength, string fieldName, List<string> notes)
    {
        if (value is null || value.Length <= maxLength)
        {
            return value;
        }

        notes.Add($"'{fieldName}' was too long ({value.Length} > {maxLength} characters) and left blank for staff review. Original value: {value}");
        return null;
    }

    // ---------- HOUSE_RENT ----------

    private static GoogleFormMappingResult MapHousing(GoogleFormAnswerLookup a, GoogleFormSubmissionRequest request, List<string> notes)
    {
        var membershipNumber = Bounded(a.GetString("Bhavnagar Jamaat Membership Number"), PakistaniFormats.JamaatMembershipMaxLength, "Bhavnagar Jamaat Membership Number", notes);
        var address = a.GetString("Current Residential Address");
        var houseStatus = HouseStatusOption(a, "House Status", notes);

        var receivedBefore = FormValueParser.ParseYesNo(a.GetOption("Has your family received housing assistance from A.Z Dany Welfare Trust before?"));

        var applicant = new MappedApplicantFields(
            Cnic: ApplicantCnic(a, "CNIC Number"),
            FullName: Bounded(a.GetString("Full Name"), 200, "Full Name", notes) ?? string.Empty,
            Email: Bounded(a.GetString("Email"), 200, "Email", notes),
            FatherOrHusbandName: Bounded(a.GetString("Father's Name"), 200, "Father's Name", notes),
            GrandfatherName: Bounded(a.GetString("Grandfather's Name"), 200, "Grandfather's Name", notes),
            Surname: Bounded(a.GetString("Surname / Identification"), 100, "Surname / Identification", notes),
            MembershipNumber: membershipNumber,
            FatherMembershipNumber: Bounded(a.GetString("Father's Bhavnagar Jamaat Membership Number"), PakistaniFormats.JamaatMembershipMaxLength, "Father's Bhavnagar Jamaat Membership Number", notes),
            AncestralVillage: Bounded(a.GetString("Ancestral Village"), 150, "Ancestral Village", notes),
            Phone: Phone(a, "Phone Number", notes),
            WhatsappNumber: null,
            Address: address,
            Occupation: Bounded(a.GetString("Business / Occupation"), 150, "Business / Occupation", notes),
            MonthlyIncome: Money(a, "Monthly Income", notes),
            HouseholdSize: Int(a, "Number of People in Household", notes),
            MaritalStatus: null);

        var housing = new UpsertHousingApplicationDetailsRequest(
            ApplicantAge: Int(a, "Age", notes),
            CurrentHouseValue: Money(a, "Current House Value (approx.)", notes),
            MonthlyRent: Money(a, "Monthly Rent (if applicable)", notes),
            AdvancePaid: Money(a, "Advance Paid (if applicable)", notes),
            YearsAtCurrentAddress: Int(a, "Years Living at Current Address", notes),
            PreviousResidentialAddress: a.GetString("Previous Residential Address"),
            ReceivedAssistanceBefore: receivedBefore,
            PreviousAssistanceDetails: a.GetString("If yes, please give details"),
            ReceivesMarriageAssistance: a.Contains("Which other assistance do you currently receive from A.Z Dany Welfare Trust?", "Marriage Assistance"),
            ReceivesEducationAssistance: a.Contains("Which other assistance do you currently receive from A.Z Dany Welfare Trust?", "Education Assistance"),
            ReceivesMedicalAssistance: a.Contains("Which other assistance do you currently receive from A.Z Dany Welfare Trust?", "Medical Assistance"),
            ReceivesWidowAssistance: a.Contains("Which other assistance do you currently receive from A.Z Dany Welfare Trust?", "Widow Assistance"));

        return new GoogleFormMappingResult(
            "HOUSE_RENT", "ZAKAT", applicant,
            Purpose: "Housing assistance request (Google Form)",
            RequestedAmount: Money(a, "Amount Requested", notes),
            DeclarationAcceptedAt: null,
            DeclaredMonthlyIncome: Money(a, "Monthly Income", notes),
            DeclaredHouseholdSize: Int(a, "Number of People in Household", notes),
            DeclaredEarningMembers: Int(a, "Number of Earning Members", notes),
            DeclaredResidentialAddress: address,
            DeclaredBusinessAddress: a.GetString("Business / Employment Address"),
            DeclaredHouseStatus: houseStatus?.ToString(),
            Housing: housing, Marriage: null, BusinessLoan: null, Education: null, Health: null,
            Guarantors: [], Files: [], Notes: notes);
    }

    // ---------- SHAADI ----------

    private static GoogleFormMappingResult MapMarriage(GoogleFormAnswerLookup a, GoogleFormSubmissionRequest request, List<string> notes)
    {
        var address = a.GetString("House Address");
        var declaration = FormValueParser.IsTicked(a.GetString("Declaration")) ? request.SubmittedAt : (DateTimeOffset?)null;

        var groomJamaatA = a.GetString("Groom's Jamaat / Community");
        var groomJamaatB = a.GetString("Groom's Jamaat");
        var groomJamaat = groomJamaatA is not null && groomJamaatB is not null && groomJamaatA != groomJamaatB
            ? $"{groomJamaatA} / {groomJamaatB}"
            : groomJamaatA ?? groomJamaatB;
        groomJamaat = Bounded(groomJamaat, 150, "Groom's Jamaat", notes);

        var applicant = new MappedApplicantFields(
            Cnic: ApplicantCnic(a, "CNIC Number"),
            FullName: Bounded(a.GetString("Applicant's (Guardian's) Name"), 200, "Applicant's (Guardian's) Name", notes) ?? string.Empty,
            Email: Bounded(a.GetString("Email"), 200, "Email", notes),
            FatherOrHusbandName: Bounded(a.GetString("Father's Name"), 200, "Father's Name", notes),
            GrandfatherName: Bounded(a.GetString("Grandfather's Name"), 200, "Grandfather's Name", notes),
            Surname: Bounded(a.GetString("Family Name"), 100, "Family Name", notes),
            MembershipNumber: Bounded(a.GetString("Bhavnagar Jamaat Membership Number"), PakistaniFormats.JamaatMembershipMaxLength, "Bhavnagar Jamaat Membership Number", notes),
            FatherMembershipNumber: null,
            AncestralVillage: null,
            Phone: Phone(a, "Mobile Number", notes),
            WhatsappNumber: null,
            Address: address,
            Occupation: null,
            MonthlyIncome: null,
            HouseholdSize: null,
            MaritalStatus: null);

        var marriage = new UpsertMarriageApplicationDetailsRequest(
            GuardianRelationshipToBride: Bounded(a.GetString("Relationship to Bride"), 100, "Relationship to Bride", notes),
            BrideName: Bounded(a.GetString("Bride's Name"), 200, "Bride's Name", notes) ?? string.Empty,
            BrideFatherName: Bounded(a.GetString("Bride's Father's Name"), 200, "Bride's Father's Name", notes),
            BrideFamilyName: Bounded(a.GetString("Bride's Family Name"), 200, "Bride's Family Name", notes),
            BrideCnic: OtherCnic(a, "Bride's CNIC Number", notes),
            BrideMaritalStatus: MaritalStatusOption(a, "Bride's Marital Status", notes)?.ToString(),
            BridePreviousHusbandName: Bounded(a.GetString("If Divorced/Widow, Previous Husband's Name"), 200, "If Divorced/Widow, Previous Husband's Name", notes),
            BrideJamaat: Bounded(a.GetString("Bride's Jamaat / Community"), 150, "Bride's Jamaat / Community", notes),
            BridePriorTrustAssistance: a.GetString("Assistance previously received from the Trust (if any)"),
            GroomName: Bounded(a.GetString("Groom's Name"), 200, "Groom's Name", notes) ?? string.Empty,
            GroomFatherName: Bounded(a.GetString("Groom's Father's Name"), 200, "Groom's Father's Name", notes),
            GroomGrandfatherName: Bounded(a.GetString("Groom's Grandfather's Name"), 200, "Groom's Grandfather's Name", notes),
            GroomJamaat: groomJamaat,
            GroomMaritalStatus: MaritalStatusOption(a, "Groom's Marital Status", notes)?.ToString(),
            GroomPreviousWifeName: Bounded(a.GetString("Previous Wife's Name (if applicable)"), 200, "Previous Wife's Name (if applicable)", notes),
            GroomAddress: a.GetString("Groom's House Address"),
            GroomMobile: Phone(a, "Groom's Mobile Number", notes),
            GroomBusinessAddress: a.GetString("Groom's Business / Employment Address"),
            NikahDate: FormValueParser.ParseDateOrNull(a.GetString("Nikah Date")),
            RukhsatiDate: FormValueParser.ParseDateOrNull(a.GetString("Rukhsati Date")));

        return new GoogleFormMappingResult(
            "SHAADI", "ZAKAT", applicant,
            Purpose: $"Marriage assistance for {marriage.BrideName}",
            RequestedAmount: Money(a, "Amount Requested", notes),
            DeclarationAcceptedAt: declaration,
            DeclaredMonthlyIncome: Money(a, "Monthly Income", notes),
            DeclaredHouseholdSize: Int(a, "Number of People in Household", notes),
            DeclaredEarningMembers: null,
            DeclaredResidentialAddress: address,
            DeclaredBusinessAddress: a.GetString("Business / Employment Address"),
            DeclaredHouseStatus: null,
            Housing: null, Marriage: marriage, BusinessLoan: null, Education: null, Health: null,
            Guarantors: [], Files: [], Notes: notes);
    }

    // ---------- ROZGAR ----------

    private static GoogleFormMappingResult MapBusinessLoan(GoogleFormAnswerLookup a, GoogleFormSubmissionRequest request, List<string> notes)
    {
        var address = a.GetString("House Address");
        var maritalRaw = a.GetOption("Marital Status (Married/Unmarried)") ?? a.GetOption("Marital Status");
        MaritalStatus? maritalStatus = maritalRaw?.Trim() switch
        {
            "Married" => MaritalStatus.Married,
            "Unmarried" => MaritalStatus.Single,
            null => null,
            _ => LogUnmapped(notes, "Marital Status", maritalRaw),
        };

        var houseStatus = HouseStatusOption(a, "House Status", notes);
        var declaration = FormValueParser.IsTicked(a.GetString("Applicant's Declaration")) ? request.SubmittedAt : (DateTimeOffset?)null;

        var businessDescriptionRaw = a.GetString("What business do you want to start? Please explain.") ?? string.Empty;

        var applicant = new MappedApplicantFields(
            Cnic: ApplicantCnic(a, "CNIC Number"),
            FullName: Bounded(a.GetString("Applicant's Name"), 200, "Applicant's Name", notes) ?? string.Empty,
            Email: Bounded(a.GetString("Email"), 200, "Email", notes),
            FatherOrHusbandName: Bounded(a.GetString("Father's Name"), 200, "Father's Name", notes),
            GrandfatherName: Bounded(a.GetString("Grandfather's Name"), 200, "Grandfather's Name", notes),
            Surname: Bounded(a.GetString("Surname"), 100, "Surname", notes),
            MembershipNumber: Bounded(a.GetString("Bhavnagar Jamaat Membership Number"), PakistaniFormats.JamaatMembershipMaxLength, "Bhavnagar Jamaat Membership Number", notes),
            FatherMembershipNumber: null,
            AncestralVillage: null,
            Phone: Phone(a, "Phone Number", notes),
            WhatsappNumber: Phone(a, "WhatsApp Number", notes),
            Address: address,
            Occupation: null,
            MonthlyIncome: null,
            HouseholdSize: null,
            MaritalStatus: maritalStatus?.ToString());

        var businessLoan = new UpsertBusinessLoanApplicationDetailsRequest(
            PaperFormNumber: null,
            BusinessPhone: Phone(a, "Business Phone Number", notes),
            Education: Bounded(a.GetString("Education"), 200, "Education", notes),
            Skill: Bounded(a.GetString("Skill"), 200, "Skill", notes),
            Experience: a.GetString("Experience"),
            OtherIncomeSources: a.GetString("Other Income Sources"),
            TotalMonthlyExpenses: Money(a, "Total Monthly Expenses", notes),
            ProposedBusinessDescription: businessDescriptionRaw,
            ProposedBusinessLocation: a.GetString("Where will you run this business? Please explain."),
            CapitalRequired: Money(a, "Capital Needed to Start Business", notes),
            CapitalAlreadyAvailable: Money(a, "Amount You Already Have", notes),
            HasPriorBusinessExperience: FormValueParser.ParseYesNo(a.GetOption("Have you done any business before?")),
            PriorBusinessDetails: a.GetString("If yes, please explain"),
            EmergencyContactName: Bounded(a.GetString("In case of accident/death, who in the family will be responsible? (Name)"), 200, "In case of accident/death, who in the family will be responsible? (Name)", notes),
            EmergencyContactCnic: OtherCnic(a, "Responsible Person's CNIC Number", notes),
            EmergencyContactPhone: Phone(a, "Responsible Person's Phone Number", notes));

        var guarantors = new List<MappedGuarantor>();
        for (var n = 1; n <= 2; n++)
        {
            var name = a.GetString($"Guarantor {n} — Name") ?? a.GetString($"Guarantor {n} - Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            guarantors.Add(new MappedGuarantor(
                SequenceNumber: n,
                MembershipNumber: Bounded(a.GetString($"Guarantor {n} — Community Bhavnagar Jamaat Membership Number"), PakistaniFormats.JamaatMembershipMaxLength, $"Guarantor {n} — Community Bhavnagar Jamaat Membership Number", notes),
                FullName: Bounded(name, 200, $"Guarantor {n} — Name", notes) ?? string.Empty,
                FatherName: Bounded(a.GetString($"Guarantor {n} — Father's Name"), 200, $"Guarantor {n} — Father's Name", notes),
                GrandfatherName: Bounded(a.GetString($"Guarantor {n} — Grandfather's Name"), 200, $"Guarantor {n} — Grandfather's Name", notes),
                Surname: Bounded(a.GetString($"Guarantor {n} — Surname"), 100, $"Guarantor {n} — Surname", notes),
                Cnic: OtherCnic(a, $"Guarantor {n} — CNIC Number", notes),
                ResidentialAddress: a.GetString($"Guarantor {n} — Current Residential Address"),
                BusinessAddress: a.GetString($"Guarantor {n} — Business Address"),
                BusinessNature: Bounded(a.GetString($"Guarantor {n} — Nature of Business"), 200, $"Guarantor {n} — Nature of Business", notes),
                // The form collects only ONE phone per guarantor — Home/Office intentionally stay
                // null (A10 blocks Approved until staff fill them in).
                PhoneMobile: Phone(a, $"Guarantor {n} — Phone (Home / Office / Mobile)", notes)));
        }

        notes.Add("The applicant's typed signature on the Business Loan form is not stored (signature capture was dropped per client decision).");

        return new GoogleFormMappingResult(
            "ROZGAR", "GENERAL", applicant,
            Purpose: $"Business loan: {Truncate(businessDescriptionRaw, 200)}",
            RequestedAmount: Money(a, "Loan Amount Needed", notes),
            DeclarationAcceptedAt: declaration,
            DeclaredMonthlyIncome: Money(a, "Total Monthly Income", notes),
            DeclaredHouseholdSize: Int(a, "Number of People Living in Household", notes),
            DeclaredEarningMembers: Int(a, "Number of Working / Earning Members", notes),
            DeclaredResidentialAddress: address,
            DeclaredBusinessAddress: a.GetString("Current Business / Employment Address"),
            DeclaredHouseStatus: houseStatus?.ToString(),
            Housing: null, Marriage: null, BusinessLoan: businessLoan, Education: null, Health: null,
            Guarantors: guarantors, Files: [], Notes: notes);
    }

    // ---------- EDUCATION ----------

    private static GoogleFormMappingResult MapEducation(GoogleFormAnswerLookup a, GoogleFormSubmissionRequest request, List<string> notes)
    {
        var address = a.GetString("Home Address");
        var fatherMonthlyIncome = Money(a, "Father's Monthly Income", notes);

        var applicant = new MappedApplicantFields(
            Cnic: ApplicantCnic(a, "Father's CNIC Number"),
            FullName: Bounded(a.GetString("Father's Name"), 200, "Father's Name", notes) ?? string.Empty,
            Email: Bounded(a.GetString("Email"), 200, "Email", notes),
            FatherOrHusbandName: Bounded(a.GetString("Father's — Grandfather's Name"), 200, "Father's — Grandfather's Name", notes),
            GrandfatherName: null,
            Surname: Bounded(a.GetString("Father's Caste / Zaat"), 100, "Father's Caste / Zaat", notes),
            MembershipNumber: Bounded(a.GetString("Father's Bhavnagar Jamaat Membership Number"), PakistaniFormats.JamaatMembershipMaxLength, "Father's Bhavnagar Jamaat Membership Number", notes),
            FatherMembershipNumber: null,
            AncestralVillage: null,
            Phone: Phone(a, "Father's Mobile Number", notes),
            WhatsappNumber: null,
            Address: address,
            Occupation: Bounded(a.GetString("Father's Skill / Profession"), 150, "Father's Skill / Profession", notes),
            MonthlyIncome: fatherMonthlyIncome,
            HouseholdSize: null,
            MaritalStatus: null);

        var education = new UpsertEducationApplicationDetailsRequest(
            CensusNumber: Bounded(a.GetString("Census No. (office reference, if known)"), 50, "Census No. (office reference, if known)", notes),
            StudentName: Bounded(a.GetString("Name of Student"), 200, "Name of Student", notes) ?? string.Empty,
            WmoId: Bounded(a.GetString("WMO ID No."), 50, "WMO ID No.", notes),
            StudentMobile: Phone(a, "Student's Mobile Number", notes),
            CurrentClass: Bounded(a.GetString("Current Class"), 50, "Current Class", notes) ?? string.Empty,
            PreviousClass: Bounded(a.GetString("Previous Class"), 50, "Previous Class", notes),
            LastExamTotalMarks: Bounded(a.GetString("Roll Number / Total Marks (last exam)"), 50, "Roll Number / Total Marks (last exam)", notes),
            LastExamMarksObtained: Bounded(a.GetString("Marks Obtained (last exam)"), 50, "Marks Obtained (last exam)", notes),
            PreviousYearAttendancePercent: Money(a, "Previous Year Attendance %", notes),
            TotalAttendanceDays: Int(a, "Total Attendance (days present)", notes),
            TotalAcademicDays: Int(a, "Total Academic Days", notes),
            FatherJamaat: Bounded(a.GetString("Father's Jamaat"), 100, "Father's Jamaat", notes),
            MotherName: Bounded(a.GetString("Mother's Name"), 200, "Mother's Name", notes) ?? string.Empty,
            MotherFatherName: Bounded(a.GetString("Mother's Father's Name"), 200, "Mother's Father's Name", notes),
            MotherCaste: Bounded(a.GetString("Mother's Caste / Zaat"), 100, "Mother's Caste / Zaat", notes),
            MotherJamaat: Bounded(a.GetString("Mother's Jamaat"), 100, "Mother's Jamaat", notes),
            MotherMembershipNumber: Bounded(a.GetString("Mother's Bhavnagar Jamaat Membership Number"), PakistaniFormats.JamaatMembershipMaxLength, "Mother's Bhavnagar Jamaat Membership Number", notes),
            MotherCnic: OtherCnic(a, "Mother's CNIC Number", notes),
            MotherMonthlyIncome: Money(a, "Mother's Monthly Income", notes),
            MotherMobile: Phone(a, "Mother's Mobile Number", notes),
            MotherProfession: Bounded(a.GetString("Mother's Skill / Profession"), 100, "Mother's Skill / Profession", notes));

        return new GoogleFormMappingResult(
            "EDUCATION", "ZAKAT", applicant,
            Purpose: a.GetString("Description(Please provide detail regarding the assisstance needed)"),
            RequestedAmount: Money(a, "Amount Requested", notes),
            DeclarationAcceptedAt: null,
            DeclaredMonthlyIncome: fatherMonthlyIncome,
            DeclaredHouseholdSize: null,
            DeclaredEarningMembers: null,
            DeclaredResidentialAddress: address,
            DeclaredBusinessAddress: null,
            DeclaredHouseStatus: null,
            Housing: null, Marriage: null, BusinessLoan: null, Education: education, Health: null,
            Guarantors: [], Files: [], Notes: notes);
    }

    // ---------- HEALTH ----------

    private static GoogleFormMappingResult MapHealth(GoogleFormAnswerLookup a, GoogleFormSubmissionRequest request, List<string> notes)
    {
        var address = a.GetString("Current Residential Address");

        var applicant = new MappedApplicantFields(
            Cnic: ApplicantCnic(a, "CNIC Number"),
            FullName: Bounded(a.GetString("Full Name"), 200, "Full Name", notes) ?? string.Empty,
            Email: Bounded(a.GetString("Email"), 200, "Email", notes),
            FatherOrHusbandName: Bounded(a.GetString("Father's Name"), 200, "Father's Name", notes),
            GrandfatherName: Bounded(a.GetString("Grandfather's Name"), 200, "Grandfather's Name", notes),
            Surname: Bounded(a.GetString("Surname / Identification"), 100, "Surname / Identification", notes),
            MembershipNumber: Bounded(a.GetString("Bhavnagar Jamaat Membership Number"), PakistaniFormats.JamaatMembershipMaxLength, "Bhavnagar Jamaat Membership Number", notes),
            FatherMembershipNumber: null,
            AncestralVillage: null,
            Phone: Phone(a, "Phone Number", notes),
            WhatsappNumber: null,
            Address: address,
            Occupation: null,
            MonthlyIncome: null,
            HouseholdSize: null,
            MaritalStatus: null);

        var health = new UpsertHealthApplicationDetailsRequest(ApplicantAge: Int(a, "Age", notes));

        return new GoogleFormMappingResult(
            "HEALTH", "ZAKAT", applicant,
            Purpose: a.GetString("Description(Please explain your health issue in details)"),
            RequestedAmount: Money(a, "Amount Requested", notes),
            DeclarationAcceptedAt: null,
            DeclaredMonthlyIncome: null,
            DeclaredHouseholdSize: Int(a, "Number of People Living in House", notes),
            DeclaredEarningMembers: null,
            DeclaredResidentialAddress: address,
            DeclaredBusinessAddress: null,
            DeclaredHouseStatus: null,
            Housing: null, Marriage: null, BusinessLoan: null, Education: null, Health: health,
            Guarantors: [], Files: [], Notes: notes);
    }

    // ---------- OTHER ----------

    private static GoogleFormMappingResult MapOther(GoogleFormAnswerLookup a, GoogleFormSubmissionRequest request, List<string> notes)
    {
        var applicant = new MappedApplicantFields(
            Cnic: ApplicantCnic(a, "CNIC NO"),
            FullName: Bounded(a.GetString("Name"), 200, "Name", notes) ?? string.Empty,
            Email: Bounded(a.GetString("Email"), 200, "Email", notes),
            FatherOrHusbandName: Bounded(a.GetString("Father Name"), 200, "Father Name", notes),
            GrandfatherName: null,
            Surname: null,
            MembershipNumber: Bounded(a.GetString("Bhavnagar Jamaat Membership Number"), PakistaniFormats.JamaatMembershipMaxLength, "Bhavnagar Jamaat Membership Number", notes),
            FatherMembershipNumber: null,
            AncestralVillage: null,
            Phone: Phone(a, "Phone", notes),
            WhatsappNumber: null,
            Address: null,
            Occupation: null,
            MonthlyIncome: null,
            HouseholdSize: null,
            MaritalStatus: null);

        return new GoogleFormMappingResult(
            "OTHER", "ZAKAT", applicant,
            Purpose: a.GetString("Description( Please explain your issue in detail )"),
            RequestedAmount: Money(a, "Amount Requested", notes),
            DeclarationAcceptedAt: null,
            DeclaredMonthlyIncome: null,
            DeclaredHouseholdSize: null,
            DeclaredEarningMembers: null,
            DeclaredResidentialAddress: null,
            DeclaredBusinessAddress: null,
            DeclaredHouseStatus: null,
            Housing: null, Marriage: null, BusinessLoan: null, Education: null, Health: null,
            Guarantors: [], Files: [], Notes: notes);
    }

    // ---------- files ----------

    /// <summary>Supported by <see cref="IDocumentService.UploadAsync"/>'s own content-sniffing —
    /// duplicated here only so <see cref="MapFiles"/> can generate the "unsupported file" note at
    /// submission time, before any file bytes have even arrived.</summary>
    private static readonly HashSet<string> SupportedFileMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "application/pdf",
    };

    private static IReadOnlyList<MappedFileTarget> MapFiles(string categoryCode, IReadOnlyList<GoogleFormFileManifestEntry> files, List<string> notes)
    {
        var targets = new List<MappedFileTarget>();

        foreach (var file in files)
        {
            if (!SupportedFileMimeTypes.Contains(file.MimeType))
            {
                notes.Add($"Unsupported file type '{file.MimeType}' for '{file.QuestionTitle}' — https://drive.google.com/open?id={file.DriveFileId} (staff follow-up needed).");
                continue;
            }

            var target = ResolveFileTarget(categoryCode, file);
            if (target is null)
            {
                notes.Add($"Unmapped file for '{file.QuestionTitle}' ({file.FileName}) — https://drive.google.com/open?id={file.DriveFileId} (staff follow-up needed).");
                continue;
            }

            targets.Add(target);
        }

        return targets;
    }

    /// <summary>The question -&gt; owner/slot/DocumentType routing table, per category — reused by
    /// <see cref="MapFiles"/> (submission-time note generation) AND by
    /// <c>GoogleFormIntakeService.UploadDocumentAsync</c> (the actual per-file upload, which arrives
    /// as a separate HTTP call once Apps Script has the Drive file). Returns null for a file this
    /// mapper has no slot for — the caller decides the fallback (a note here, an unslotted
    /// application-owned SupportingDocument there).</summary>
    public static MappedFileTarget? ResolveFileTarget(string categoryCode, GoogleFormFileManifestEntry file)
    {
        var normalizedTitle = FormValueParser.NormalizeTitle(file.QuestionTitle);
        var helpText = file.QuestionHelpText is null ? null : FormValueParser.NormalizeTitle(file.QuestionHelpText);
        var isPdf = string.Equals(file.MimeType, "application/pdf", StringComparison.OrdinalIgnoreCase);

        MappedFileTarget? Resolve(string title) => categoryCode switch
        {
                "HOUSE_RENT" => title switch
                {
                    "Attach: Bhavnagar Jamaat membership card" => Applicant(file, DocumentType.MembershipCard),
                    "Attach: photocopy of CNIC" => Applicant(file, DocumentType.CnicFront),
                    _ => null,
                },
                "SHAADI" => title switch
                {
                    // 2026-09 correction: these three are the BRIDE's documents (confirmed by the
                    // client directly), not the applicant/guardian's — see the wedding-card slot too.
                    "Attach:original Marriage/Nikah card" => Application(file, DocumentType.WeddingCard, "SHAADI.WEDDING_CARD"),
                    "Attach: photocopy of Bhavnagar Jamaat membership card" => Application(file, DocumentType.MembershipCard, "SHAADI.BRIDE_MEMBERSHIP_CARD"),
                    "Attach: photocopy of CNIC" => Application(file, DocumentType.CnicFront, "SHAADI.BRIDE_CNIC_OR_BFORM"),
                    _ => null,
                },
                "ROZGAR" => title switch
                {
                    "Required documents: Attach: CNIC or Form B" => Applicant(file, DocumentType.CnicFront),
                    _ when title.Contains("Jamaat card copies", StringComparison.OrdinalIgnoreCase) => Applicant(file, DocumentType.MembershipCard),
                    _ when title.Contains("Guarantor1 CNIC", StringComparison.OrdinalIgnoreCase) => Guarantor(file, DocumentType.CnicFront, "ROZGAR.GUARANTOR_CNIC", 1),
                    _ when title.Contains("Guarantor2 CNIC", StringComparison.OrdinalIgnoreCase) => Guarantor(file, DocumentType.CnicFront, "ROZGAR.GUARANTOR_CNIC", 2),
                    _ when title.Contains("Guarantor1 Jamaat Card", StringComparison.OrdinalIgnoreCase) => Guarantor(file, DocumentType.MembershipCard, "ROZGAR.GUARANTOR_MEMBERSHIP_CARD", 1),
                    _ when title.Contains("Guarantor2 Jamaat Card", StringComparison.OrdinalIgnoreCase) => Guarantor(file, DocumentType.MembershipCard, "ROZGAR.GUARANTOR_MEMBERSHIP_CARD", 2),
                    // The combined loan-application/business-writeup/photos/utility-bill upload
                    // can't be auto-sorted into individual slots — stored unslotted for staff to
                    // manually re-slot, per the user's confirmed decision.
                    _ when title.Contains("loan application", StringComparison.OrdinalIgnoreCase) =>
                        Application(file, DocumentType.SupportingDocument, null, "Google Form combined upload: loan application / business write-up / photos / utility bill"),
                    _ => null,
                },
                "EDUCATION" => helpText switch
                {
                    { } h when h.Contains("Jamaat Card", StringComparison.OrdinalIgnoreCase) => Applicant(file, DocumentType.MembershipCard),
                    { } h when h.Contains("B-Form or Cnic", StringComparison.OrdinalIgnoreCase) => Application(file, DocumentType.FormB, "EDUCATION.STUDENT_BFORM_OR_CNIC"),
                    { } h when h.Contains("passport-size photo", StringComparison.OrdinalIgnoreCase) =>
                        Application(file, isPdf ? DocumentType.AcademicResult : DocumentType.PassportPhoto, "EDUCATION.STUDENT_PHOTO_AND_RESULT"),
                    _ => null,
                },
                "HEALTH" => title switch
                {
                    "File Upload(Please attach supporting Medical Documents)" => Application(file, DocumentType.MedicalReport, "HEALTH.MEDICAL_DOCUMENTS"),
                    "File Upload CNIC" => Applicant(file, DocumentType.CnicFront),
                    "File Upload(BhavnagarJamaat Membership Card)" => Applicant(file, DocumentType.MembershipCard),
                    _ => null,
                },
                "OTHER" => title switch
                {
                    "File Upload(Please upload Cnic)" => Applicant(file, DocumentType.CnicFront),
                    "File Upload(Please upload Jamaat Membership card )" => Applicant(file, DocumentType.MembershipCard),
                    "File Upload( Please upload supporting documents )" => Application(file, DocumentType.SupportingDocument, "OTHER.SUPPORTING_DOCUMENTS"),
                    _ => null,
                },
                _ => null,
            };

        // D: an upload question's TITLE itself (not an options list) sometimes carries an inline
        // Urdu parenthetical (Housing's "Attach: ... (اردو)") — try the raw normalized title first,
        // then fall back to the title with a trailing " (...)" stripped.
        return Resolve(normalizedTitle) ?? Resolve(FormValueParser.StripOptionParenthetical(normalizedTitle));

        static MappedFileTarget Applicant(GoogleFormFileManifestEntry file, DocumentType type) =>
            new(file, DocumentSlotOwnerScope.Applicant, type, null, null);

        static MappedFileTarget Application(GoogleFormFileManifestEntry file, DocumentType type, string? slotKey, string? description = null) =>
            new(file, DocumentSlotOwnerScope.Application, type, slotKey, null, description);

        static MappedFileTarget Guarantor(GoogleFormFileManifestEntry file, DocumentType type, string slotKey, int sequenceNumber) =>
            new(file, DocumentSlotOwnerScope.Guarantor, type, slotKey, sequenceNumber);
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    private static MaritalStatus? LogUnmapped(List<string> notes, string title, string raw)
    {
        notes.Add($"Could not map '{title}' value '{raw}' to a marital status.");
        return null;
    }
}
