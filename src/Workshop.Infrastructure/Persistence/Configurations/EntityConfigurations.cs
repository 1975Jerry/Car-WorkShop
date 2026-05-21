using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workshop.Domain.Entities.CrossCutting;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Entities.Retail;
using Workshop.Domain.Entities.Shared;

namespace Workshop.Infrastructure.Persistence.Configurations;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> e)
    {
        e.HasIndex(x => x.Code).IsUnique();
        e.HasOne(x => x.Warehouse).WithOne(w => w.Branch).HasForeignKey<Warehouse>(w => w.BranchId);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> e)
    {
        e.HasIndex(x => x.MobilePhone);
        e.HasIndex(x => x.VatNumber);
    }
}

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> e)
    {
        e.HasIndex(x => x.PlateNumber).IsUnique();
        e.HasIndex(x => x.Vin);
        e.HasOne(x => x.Customer).WithMany(c => c.Vehicles).HasForeignKey(x => x.CustomerId);
    }
}

public class InsuranceCompanyConfiguration : IEntityTypeConfiguration<InsuranceCompany>
{
    public void Configure(EntityTypeBuilder<InsuranceCompany> e)
    {
        e.HasIndex(x => x.Name).IsUnique();
    }
}

public class BodyPanelConfiguration : IEntityTypeConfiguration<BodyPanel>
{
    public void Configure(EntityTypeBuilder<BodyPanel> e)
    {
        e.HasIndex(x => x.Code).IsUnique();
        // Diagram coordinates are SVG-friendly percentages, so 6,2 is plenty.
        e.Property(x => x.DiagramX).HasPrecision(6, 2);
        e.Property(x => x.DiagramY).HasPrecision(6, 2);
        e.HasMany(x => x.AllowedOperations).WithOne(o => o.BodyPanel).HasForeignKey(o => o.BodyPanelId);
    }
}

public class BodyPanelOperationConfiguration : IEntityTypeConfiguration<BodyPanelOperation>
{
    public void Configure(EntityTypeBuilder<BodyPanelOperation> e)
    {
        e.HasKey(x => new { x.BodyPanelId, x.Operation });
    }
}

public class InsuranceCaseConfiguration : IEntityTypeConfiguration<InsuranceCase>
{
    public void Configure(EntityTypeBuilder<InsuranceCase> e)
    {
        e.HasIndex(x => x.CaseNumber).IsUnique();
        e.HasIndex(x => new { x.BranchId, x.Status });
        e.Property(x => x.Notes).HasColumnType("text");
        e.HasOne(x => x.Assessment).WithOne(a => a.InsuranceCase).HasForeignKey<Assessment>(a => a.InsuranceCaseId);
        e.HasOne(x => x.Approval).WithOne(a => a.InsuranceCase).HasForeignKey<InsuranceApproval>(a => a.InsuranceCaseId);
        e.HasOne(x => x.Repair).WithOne(r => r.InsuranceCase).HasForeignKey<Repair>(r => r.InsuranceCaseId);
    }
}

public class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> e)
    {
        e.Property(x => x.LaborCost).HasPrecision(12, 2);
        e.Property(x => x.PartsCost).HasPrecision(12, 2);
        e.Property(x => x.PaintMaterialsCost).HasPrecision(12, 2);
        e.Property(x => x.TotalEstimatedCost).HasPrecision(12, 2);
        e.Property(x => x.AgreedAmount).HasPrecision(12, 2);
    }
}

public class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> e)
    {
        e.Property(x => x.Cost_Polish).HasPrecision(12, 2);
        e.Property(x => x.Cost_PDR).HasPrecision(12, 2);
        e.Property(x => x.Cost_RemoveRefit).HasPrecision(12, 2);
        e.Property(x => x.Cost_Replace).HasPrecision(12, 2);
        e.Property(x => x.Cost_DisassembleAssemble).HasPrecision(12, 2);
        e.Property(x => x.Cost_Repair).HasPrecision(12, 2);
        e.Property(x => x.Cost_Paint).HasPrecision(12, 2);
        e.Property(x => x.Cost_RepairPaint).HasPrecision(12, 2);
        e.Property(x => x.Cost_Weld).HasPrecision(12, 2);
        e.Property(x => x.Cost_Other).HasPrecision(12, 2);
        e.Property(x => x.DiscountPct).HasPrecision(5, 2);
        e.Property(x => x.Total).HasPrecision(12, 2);
    }
}

public class InsurancePartLineConfiguration : IEntityTypeConfiguration<InsurancePartLine>
{
    public void Configure(EntityTypeBuilder<InsurancePartLine> e)
    {
        e.Property(x => x.Quantity).HasPrecision(10, 2);
        e.Property(x => x.UnitCost).HasPrecision(12, 2);
        e.Property(x => x.DiscountPct).HasPrecision(5, 2);
        e.Property(x => x.Total).HasPrecision(12, 2);
        e.HasOne(x => x.DestinationBranch).WithMany().HasForeignKey(x => x.DestinationBranchId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> e)
    {
        e.HasIndex(x => x.QuoteNumber).IsUnique();
        e.Property(x => x.LaborSubtotal).HasPrecision(12, 2);
        e.Property(x => x.PartsSubtotal).HasPrecision(12, 2);
        e.Property(x => x.LaborDiscountAmount).HasPrecision(12, 2);
        e.Property(x => x.PartsDiscountAmount).HasPrecision(12, 2);
        e.Property(x => x.Subtotal).HasPrecision(12, 2);
        e.Property(x => x.VatRate).HasPrecision(5, 2);
        e.Property(x => x.VatAmount).HasPrecision(12, 2);
        e.Property(x => x.Total).HasPrecision(12, 2);
        e.Property(x => x.CustomerParticipation).HasPrecision(12, 2);
    }
}

public class InsuranceApprovalConfiguration : IEntityTypeConfiguration<InsuranceApproval>
{
    public void Configure(EntityTypeBuilder<InsuranceApproval> e)
    {
        e.Property(x => x.ParticipationAmount).HasPrecision(12, 2);
        e.Property(x => x.ApprovedAmount).HasPrecision(12, 2);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> e)
    {
        e.Property(x => x.Amount).HasPrecision(12, 2);
    }
}

public class RetailCaseConfiguration : IEntityTypeConfiguration<RetailCase>
{
    public void Configure(EntityTypeBuilder<RetailCase> e)
    {
        e.HasIndex(x => x.CaseNumber).IsUnique();
        e.HasIndex(x => new { x.BranchId, x.Status });
        e.Property(x => x.FinalCost).HasPrecision(12, 2);
        e.Property(x => x.VatAmount).HasPrecision(12, 2);
        e.Property(x => x.TotalWithVat).HasPrecision(12, 2);
        e.HasOne(x => x.Repair).WithOne(r => r.RetailCase).HasForeignKey<RetailRepair>(r => r.RetailCaseId);
    }
}

public class RetailCasePanelConfiguration : IEntityTypeConfiguration<RetailCasePanel>
{
    public void Configure(EntityTypeBuilder<RetailCasePanel> e)
    {
        e.HasIndex(x => new { x.RetailCaseId, x.BodyPanelId }).IsUnique();
        e.HasOne(x => x.RetailCase).WithMany(c => c.DamagedPanels)
            .HasForeignKey(x => x.RetailCaseId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.BodyPanel).WithMany()
            .HasForeignKey(x => x.BodyPanelId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RetailPartLineConfiguration : IEntityTypeConfiguration<RetailPartLine>
{
    public void Configure(EntityTypeBuilder<RetailPartLine> e)
    {
        e.Property(x => x.Quantity).HasPrecision(10, 2);
        e.Property(x => x.UnitCost).HasPrecision(12, 2);
        e.Property(x => x.Total).HasPrecision(12, 2);
        e.HasOne(x => x.DestinationBranch).WithMany().HasForeignKey(x => x.DestinationBranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ServiceCatalogConfiguration : IEntityTypeConfiguration<ServiceCatalog>
{
    public void Configure(EntityTypeBuilder<ServiceCatalog> e)
    {
        e.HasIndex(x => x.Code).IsUnique();
        e.Property(x => x.DefaultPrice).HasPrecision(12, 2);
    }
}

public class CompanyProfileConfiguration : IEntityTypeConfiguration<CompanyProfile>
{
    public void Configure(EntityTypeBuilder<CompanyProfile> e)
    {
        e.Property(x => x.DefaultVatRate).HasPrecision(5, 2);
    }
}

public class LoginAuditEntryConfiguration : IEntityTypeConfiguration<LoginAuditEntry>
{
    public void Configure(EntityTypeBuilder<LoginAuditEntry> e)
    {
        e.HasIndex(x => x.OccurredAt).IsDescending();
        e.HasIndex(x => x.Email);
        e.Property(x => x.Email).HasMaxLength(256).IsRequired();
        e.Property(x => x.IpAddress).HasMaxLength(64);
        e.Property(x => x.UserAgent).HasMaxLength(512);
        e.Property(x => x.FailureReason).HasMaxLength(256);
        // SetNull so deleting a user (soft delete keeps row anyway, but defense in depth)
        // doesn't cascade-delete their audit history.
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
