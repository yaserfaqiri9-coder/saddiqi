using PTGOilSystem.Web.Models.PartyStatements;

namespace PTGOilSystem.Web.Services.PartyStatements;

/// <summary>
/// قرارداد نمایشیِ یک‌دستِ صورت‌حساب طرف‌حساب‌ها — مطابق فایل کاری Petrogas:
///
///   ستون بستانکار = آنچه شرکت به طرف داده (پرداخت)،
///   ستون بدهکار   = آنچه شرکت از طرف گرفته (بار/فاکتور/خدمت)،
///   مانده          = Σ(بستانکار − بدهکار)،
///   مانده مثبت     = شرکت پیش‌پرداخت دارد (طلب از طرف).
///
/// این فقط لایهٔ نمایش است. جدول LedgerEntry و دفتر کل جدید (JournalEntryLine) با
/// قرارداد حسابداری استاندارد خود دست‌نخورده باقی می‌مانند؛ وارونه‌سازی هنگام خواندن
/// در PartyStatementReadService.MapLedgerRow با ReverseLegacyLedgerSides اعمال می‌شود.
///
/// نکته: این پرچم فقط روی طرف‌حساب‌هایی اثر دارد که از MapLedgerRow عبور می‌کنند
/// (مشتری، تأمین‌کننده، شرکت خدماتی، راننده، شرکت، شریک). صراف و کارمند سازندهٔ
/// اختصاصی دارند و پرچم را نمی‌خوانند، بنابراین معانی آن‌ها دست‌نخورده مانده است.
/// </summary>
public sealed class PartyStatementPolicyResolver : IPartyStatementPolicyResolver
{
    private static readonly IReadOnlyDictionary<PartyStatementPartyType, PartyStatementPolicy> Policies =
        new Dictionary<PartyStatementPartyType, PartyStatementPolicy>
        {
            [PartyStatementPartyType.Customer] = new()
            {
                PartyType = PartyStatementPartyType.Customer,
                StatementTitleFa = "صورت‌حساب مشتری",
                StatementTitleEn = "Customer Statement",
                PartyInformationTitleFa = "اطلاعات مشتری",
                PartyInformationTitleEn = "Customer Information",
                AccountTypeFa = "حساب دریافتنی / پیش‌دریافت",
                DebitMeaningFa = "فروش یا تحویل به مشتری",
                CreditMeaningFa = "پرداخت یا دریافت از مشتری",
                PositiveBalanceMeaningFa = "مشتری نزد شرکت اعتبار یا پیش‌پرداخت دارد",
                NegativeBalanceMeaningFa = "مبلغ قابل دریافت از مشتری",
                ReverseLegacyLedgerSides = true,
                SupportsOperationalColumns = true
            },
            [PartyStatementPartyType.Supplier] = Payable(
                PartyStatementPartyType.Supplier,
                "صورت‌حساب تأمین‌کننده",
                "Supplier Statement",
                "اطلاعات تأمین‌کننده",
                "Supplier Information",
                "شرکت نزد تأمین‌کننده پیش‌پرداخت دارد",
                "شرکت به تأمین‌کننده بدهکار است",
                supportsOperationalColumns: true),
            [PartyStatementPartyType.ServiceProvider] = Payable(
                PartyStatementPartyType.ServiceProvider,
                "صورت‌حساب شرکت خدماتی",
                "Service Provider Statement",
                "اطلاعات شرکت خدماتی",
                "Service Provider Information",
                "پیش‌پرداخت شرکت نزد ارائه‌دهنده خدمت",
                "مبلغ قابل پرداخت به شرکت خدماتی"),
            // صراف و کارمند سازندهٔ سطر اختصاصی دارند و ReverseLegacyLedgerSides را
            // نمی‌خوانند؛ بنابراین قرارداد و معانی قبلی آن‌ها دست‌نخورده می‌ماند.
            [PartyStatementPartyType.Sarraf] = Payable(
                PartyStatementPartyType.Sarraf,
                "صورت‌حساب صراف",
                "Sarraf Statement",
                "اطلاعات صراف",
                "Sarraf Information",
                "مبلغ قابل پرداخت به صراف",
                "مبلغ قابل دریافت از صراف",
                reverseLegacyLedgerSides: false),
            [PartyStatementPartyType.Employee] = Payable(
                PartyStatementPartyType.Employee,
                "صورت‌حساب کارمند",
                "Employee Statement",
                "اطلاعات کارمند",
                "Employee Information",
                "مبلغ قابل پرداخت به کارمند",
                "پیش‌پرداخت یا مبلغ قابل دریافت از کارمند",
                reverseLegacyLedgerSides: false),
            [PartyStatementPartyType.Partner] = Payable(
                PartyStatementPartyType.Partner,
                "صورت‌حساب سهم شریک",
                "Partner Share Statement",
                "اطلاعات شریک",
                "Partner Information",
                "سهم بدهکار شریک",
                "سهم بستانکار شریک",
                supportsOperationalColumns: true,
                accountTypeFa: "حساب سهم و سرمایه شریک"),
            [PartyStatementPartyType.Driver] = Payable(
                PartyStatementPartyType.Driver,
                "صورت‌حساب راننده",
                "Driver Statement",
                "اطلاعات راننده",
                "Driver Information",
                "مبلغ قابل دریافت از راننده",
                "مبلغ قابل پرداخت به راننده"),
            [PartyStatementPartyType.Company] = Payable(
                PartyStatementPartyType.Company,
                "صورت‌حساب شرکت",
                "Company Statement",
                "اطلاعات شرکت",
                "Company Information",
                "مانده بدهکار شرکت",
                "مانده بستانکار شرکت",
                supportsOperationalColumns: true,
                accountTypeFa: "حساب جاری شرکت")
        };

    public PartyStatementPolicy Resolve(PartyStatementPartyType partyType)
        => Policies.TryGetValue(partyType, out var policy)
            ? policy
            : throw new ArgumentOutOfRangeException(nameof(partyType), partyType, "نوع طرف‌حساب پشتیبانی نمی‌شود.");

    private static PartyStatementPolicy Payable(
        PartyStatementPartyType type,
        string titleFa,
        string titleEn,
        string infoTitleFa,
        string infoTitleEn,
        string positiveMeaning,
        string negativeMeaning,
        bool supportsOperationalColumns = false,
        string accountTypeFa = "حساب پرداختنی",
        bool reverseLegacyLedgerSides = true)
        => new()
        {
            PartyType = type,
            StatementTitleFa = titleFa,
            StatementTitleEn = titleEn,
            PartyInformationTitleFa = infoTitleFa,
            PartyInformationTitleEn = infoTitleEn,
            AccountTypeFa = accountTypeFa,
            // با وارونه‌سازی، ستون بدهکار «آنچه گرفتیم» و ستون بستانکار «آنچه دادیم» را
            // نشان می‌دهد؛ بدون آن، معنای حسابداری قبلی برقرار می‌ماند.
            DebitMeaningFa = reverseLegacyLedgerSides ? "ایجاد یا افزایش بدهی" : "پرداخت یا کاهش بدهی",
            CreditMeaningFa = reverseLegacyLedgerSides ? "پرداخت یا کاهش بدهی" : "ایجاد یا افزایش بدهی",
            PositiveBalanceMeaningFa = positiveMeaning,
            NegativeBalanceMeaningFa = negativeMeaning,
            ReverseLegacyLedgerSides = reverseLegacyLedgerSides,
            SupportsOperationalColumns = supportsOperationalColumns
        };
}
