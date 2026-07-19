using PTGOilSystem.Web.Models.PartyStatements;

namespace PTGOilSystem.Web.Services.PartyStatements;

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
                "شرکت به تأمین‌کننده بدهکار است",
                "شرکت نزد تأمین‌کننده پیش‌پرداخت دارد",
                supportsOperationalColumns: true),
            [PartyStatementPartyType.ServiceProvider] = Payable(
                PartyStatementPartyType.ServiceProvider,
                "صورت‌حساب شرکت خدماتی",
                "Service Provider Statement",
                "اطلاعات شرکت خدماتی",
                "Service Provider Information",
                "مبلغ قابل پرداخت به شرکت خدماتی",
                "پیش‌پرداخت شرکت نزد ارائه‌دهنده خدمت"),
            [PartyStatementPartyType.Sarraf] = Payable(
                PartyStatementPartyType.Sarraf,
                "صورت‌حساب صراف",
                "Sarraf Statement",
                "اطلاعات صراف",
                "Sarraf Information",
                "مبلغ قابل پرداخت به صراف",
                "مبلغ قابل دریافت از صراف"),
            [PartyStatementPartyType.Employee] = Payable(
                PartyStatementPartyType.Employee,
                "صورت‌حساب کارمند",
                "Employee Statement",
                "اطلاعات کارمند",
                "Employee Information",
                "مبلغ قابل پرداخت به کارمند",
                "پیش‌پرداخت یا مبلغ قابل دریافت از کارمند"),
            [PartyStatementPartyType.Partner] = Payable(
                PartyStatementPartyType.Partner,
                "صورت‌حساب سهم شریک",
                "Partner Share Statement",
                "اطلاعات شریک",
                "Partner Information",
                "سهم بستانکار شریک",
                "سهم بدهکار شریک",
                supportsOperationalColumns: true,
                accountTypeFa: "حساب سهم و سرمایه شریک"),
            [PartyStatementPartyType.Driver] = Payable(
                PartyStatementPartyType.Driver,
                "صورت‌حساب راننده",
                "Driver Statement",
                "اطلاعات راننده",
                "Driver Information",
                "مبلغ قابل پرداخت به راننده",
                "مبلغ قابل دریافت از راننده"),
            [PartyStatementPartyType.Company] = Payable(
                PartyStatementPartyType.Company,
                "صورت‌حساب شرکت",
                "Company Statement",
                "اطلاعات شرکت",
                "Company Information",
                "مانده بستانکار شرکت",
                "مانده بدهکار شرکت",
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
        string accountTypeFa = "حساب پرداختنی")
        => new()
        {
            PartyType = type,
            StatementTitleFa = titleFa,
            StatementTitleEn = titleEn,
            PartyInformationTitleFa = infoTitleFa,
            PartyInformationTitleEn = infoTitleEn,
            AccountTypeFa = accountTypeFa,
            DebitMeaningFa = "پرداخت یا کاهش بدهی",
            CreditMeaningFa = "ایجاد یا افزایش بدهی",
            PositiveBalanceMeaningFa = positiveMeaning,
            NegativeBalanceMeaningFa = negativeMeaning,
            SupportsOperationalColumns = supportsOperationalColumns
        };
}
