namespace NguyenBinh.Domain.Content;

/// <summary>Loai noi dung cua du an (muc 5). Mot du an co the co nhieu loai.</summary>
public enum ProjectContentType
{
    CustomProject,
    OwnProduct,
    CommercialProduct,
    InternalProduct,
    PartnerSolution,
    CaseStudy,
    Website,
    MobileApp,
    Saas,
    EnterpriseSoftware,
    Ecommerce,
    Pos,
    Pms,
    Erp,
    Hrm,
    Other,
}

public enum CommercialType
{
    ForSale,
    CustomDevelopment,
    License,
    SaasSubscription,
    ImplementationService,
    InternalOnly,
    ShowcaseOnly,
    ContactForPrice,
}

/// <summary>Vai tro cua Nguyen Binh trong du an. Mot du an co the co nhieu vai tro.</summary>
public enum ProjectRole
{
    Owner,
    Developer,
    CoDeveloper,
    TechnicalPartner,
    ImplementationPartner,
    Outsourcing,
    Maintenance,
    UiUx,
    Backend,
    Frontend,
    Mobile,
    Fullstack,
    Other,
}

/// <summary>
/// Quyen so huu (muc 25). Chi NguyenBinhOwned moi duoc goi la "San pham cua Nguyen Binh";
/// Undisclosed = chua cau hinh → khong duoc xuat ban.
/// </summary>
public enum OwnershipType
{
    Undisclosed,
    NguyenBinhOwned,
    ClientOwned,
    CoOwned,
    PartnerOwned,
}

public enum ProjectState
{
    InProgress,
    Live,
    Maintenance,
    Ended,
}

public enum ProjectMediaKind
{
    Desktop,
    Mobile,
    Dashboard,
    Before,
    After,
    Gallery,
    Video,
    YouTube,
    Vimeo,
    ExternalVideo,
    Architecture,
    Pdf,
    ClientLogo,
    Feature,
}

public enum ProjectLinkKind
{
    Website,
    AppStore,
    GooglePlay,
    Demo,
    Docs,
    Other,
}

public enum ProductType
{
    Pos,
    Pms,
    Erp,
    Hrm,
    Crm,
    Saas,
    Mobile,
    Other,
}

public enum BillingPeriod
{
    OneTime,
    Monthly,
    Yearly,
    Contact,
}

public enum TechnologyGroup
{
    Backend,
    Frontend,
    Mobile,
    Data,
    Infrastructure,
    Other,
}

public enum FaqScope
{
    Global,
    Service,
    Solution,
    Product,
}

public enum PageType
{
    Home,
    Standard,
    Landing,
    Solution,
    Legal,
}
