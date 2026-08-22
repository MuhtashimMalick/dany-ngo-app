namespace NgoFund.Domain.Enums;

public enum DocumentType
{
    /// <summary>The applicant's profile photo, whether webcam-captured or uploaded from a file —
    /// printed passport-size photographs collected as application supporting documents
    /// (<c>ROZGAR.PASSPORT_PHOTOS</c> slot) remain specifically <see cref="PassportPhoto"/>.</summary>
    ApplicantPhoto,
    CnicFront,
    CnicBack,
    SupportingDocument,
    DonorReceipt,
    PaymentProof,
    Other,
    MembershipCard,
    SignedApplicationForm,
    /// <summary>The Nikahnama issued at/after the nikah — the wedding invitation card is <see cref="WeddingCard"/>, a different physical document at a different point in time.</summary>
    NikahCertificate,
    BusinessPlan,
    UtilityBill,
    FormB,
    PassportPhoto,
    WeddingCard,
    RentReceipt
}
