using Microsoft.EntityFrameworkCore;
using Workshop.Domain.Entities.CrossCutting;
using Workshop.Domain.Entities.Identity;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Entities.Retail;
using Workshop.Domain.Entities.Shared;

namespace Workshop.Application.Common.Abstractions;

public interface IWorkshopDbContext
{
    // Shared
    DbSet<CompanyProfile> CompanyProfiles { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<InsuranceCompany> InsuranceCompanies { get; }
    DbSet<Assessor> Assessors { get; }
    DbSet<Adjuster> Adjusters { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<BodyPanel> BodyPanels { get; }
    DbSet<BodyPanelOperation> BodyPanelOperations { get; }
    DbSet<ServiceCatalog> ServiceCatalogs { get; }

    // Insurance
    DbSet<InsuranceCase> InsuranceCases { get; }
    DbSet<Assessment> Assessments { get; }
    DbSet<WorkItem> WorkItems { get; }
    DbSet<InsurancePartLine> InsurancePartLines { get; }
    DbSet<InsuranceApproval> InsuranceApprovals { get; }
    DbSet<Quote> Quotes { get; }
    DbSet<Repair> Repairs { get; }
    DbSet<Photo> Photos { get; }
    DbSet<Document> Documents { get; }
    DbSet<Payment> Payments { get; }

    // Retail
    DbSet<RetailCase> RetailCases { get; }
    DbSet<RetailPartLine> RetailPartLines { get; }
    DbSet<RetailRepair> RetailRepairs { get; }

    // Users (read-only views — Identity-managed)
    DbSet<User> Users { get; }

    // Cross-cutting
    DbSet<CaseEvent> CaseEvents { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
