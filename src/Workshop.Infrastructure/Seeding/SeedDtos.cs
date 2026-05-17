namespace Workshop.Infrastructure.Seeding;

internal record BodyPanelSeed(string Code, string DescriptionGr, string? DescriptionEn, string Category, string Side, decimal? DiagramX, decimal? DiagramY);
internal record BodyPanelOperationSeed(string PanelCode, string Operation);
internal record ServiceCatalogSeed(string Code, string NameGr, string? NameEn, decimal? DefaultPrice);
internal record InsuranceCompanySeed(string Name, string? Phone, string? Email, string? AddressLine, string? VatNumber);
internal record CompanyProfileSeed(string Name, string AddressLine, string City, string? PostalCode, string Phone, string? Email, string VatNumber, string? TaxOffice, string? LogoPath, decimal DefaultVatRate);
internal record BranchSeed(string Name, string Code, string AddressLine, string City, string? PostalCode, string? Phone, WarehouseSeed Warehouse);
internal record WarehouseSeed(string Name, string? Description);
