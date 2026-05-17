using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Workshop.Domain.Entities.CrossCutting;
using Workshop.Domain.Entities.Identity;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Entities.Retail;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Infrastructure.Seeding;

public partial class SeedRunner
{
    // Seeds three cases per workflow stage for both Insurance and Retail flows, with
    // cumulative child data so the UI looks realistic at each stage. Idempotent: skips
    // entirely if any insurance or retail cases already exist (so user-created data
    // doesn't get diluted).
    private async Task SeedDemoCasesAsync(CancellationToken ct)
    {
        var demoAlreadySeeded =
            await _db.InsuranceCases.AnyAsync(c => c.CaseNumber.StartsWith("INS-DEMO-"), ct) ||
            await _db.RetailCases.AnyAsync(c => c.CaseNumber.StartsWith("RET-DEMO-"), ct);
        if (demoAlreadySeeded)
        {
            _log.LogInformation("Demo cases skipped: INS-DEMO/RET-DEMO rows already present.");
            return;
        }

        var branches = await _db.Branches
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name).ToListAsync(ct);
        if (branches.Count == 0)
        {
            _log.LogWarning("Demo cases skipped: no branches exist.");
            return;
        }
        var insurers = await _db.InsuranceCompanies.OrderBy(c => c.Name).ToListAsync(ct);
        if (insurers.Count == 0)
        {
            _log.LogWarning("Demo cases skipped: no insurance companies exist.");
            return;
        }
        var panels = await _db.BodyPanels
            .Where(p => p.Category == BodyPanelCategory.External)
            .OrderBy(p => p.Code).Take(20).ToListAsync(ct);
        var adminUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == "admin@paintbull.local", ct);
        if (adminUser is null)
        {
            _log.LogWarning("Demo cases skipped: admin user missing (needed as Quote.ResponsibleUserId).");
            return;
        }

        var rng = new Random(42);
        var customers = await EnsureDemoCustomerPoolAsync(count: 12, rng, ct);
        var vehicles = await EnsureDemoVehiclePoolAsync(customers, insurers, rng, ct);

        var seq = 0;

        foreach (var status in Enum.GetValues<InsuranceCaseStatus>())
        {
            for (var i = 0; i < 3; i++)
            {
                seq++;
                BuildInsuranceCaseAtStage(
                    status,
                    caseNumber: $"INS-DEMO-{seq:D4}",
                    customer: PickFrom(customers, rng),
                    vehicle: PickFrom(vehicles, rng),
                    branch: branches[seq % branches.Count],
                    insurer: PickFrom(insurers, rng),
                    panels: panels,
                    responsibleUserId: adminUser.Id,
                    rng: rng);
            }
        }

        foreach (var status in Enum.GetValues<RetailCaseStatus>())
        {
            for (var i = 0; i < 3; i++)
            {
                seq++;
                BuildRetailCaseAtStage(
                    status,
                    caseNumber: $"RET-DEMO-{seq:D4}",
                    customer: PickFrom(customers, rng),
                    vehicle: PickFrom(vehicles, rng),
                    branch: branches[seq % branches.Count],
                    rng: rng);
            }
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Demo cases seeded: 36 insurance + 18 retail across all workflow stages.");
    }

    private void BuildInsuranceCaseAtStage(
        InsuranceCaseStatus targetStage,
        string caseNumber,
        Customer customer,
        Vehicle vehicle,
        Branch branch,
        InsuranceCompany insurer,
        IReadOnlyList<BodyPanel> panels,
        Guid responsibleUserId,
        Random rng)
    {
        var createdAt = RandomPastDate(rng, daysBack: 60);
        var case_ = new InsuranceCase
        {
            CaseNumber = caseNumber,
            Customer = customer,
            Vehicle = vehicle,
            Branch = branch,
            InsuranceCompany = insurer,
            ClaimNumber = $"CL-{rng.Next(100000, 999999)}",
            Status = targetStage,
            Priority = (CasePriority)rng.Next(1, 5),
            DriverFirstName = customer.FirstName,
            DriverLastName = customer.LastName,
            DriverPhone = customer.MobilePhone,
            AccidentDate = DateOnly.FromDateTime(createdAt.AddDays(-rng.Next(0, 7))),
            MileageAtAssessment = 30_000 + rng.Next(0, 100_000),
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
        _db.InsuranceCases.Add(case_);

        var stageNum = (int)targetStage;

        // From Assessment (3) onwards: assessment + work items
        Assessment? assessment = null;
        if (stageNum >= (int)InsuranceCaseStatus.Assessment)
        {
            var laborItems = panels.OrderBy(_ => rng.Next()).Take(rng.Next(2, 5)).ToList();
            assessment = new Assessment
            {
                InsuranceCase = case_,
                AssessmentDate = DateOnly.FromDateTime(createdAt.AddDays(2)),
                AgreementDate = DateOnly.FromDateTime(createdAt.AddDays(3)),
                LaborCost = 0m,
                PartsRequired = true,
                PaintMaterialsCost = 80m + rng.Next(0, 220),
                IntermediateInspection = false,
                Notes = "Auto-generated demo assessment",
                CreatedAt = createdAt.AddDays(2),
                UpdatedAt = createdAt.AddDays(2)
            };
            foreach (var panel in laborItems)
            {
                var paintCost = 60m + rng.Next(0, 180);
                var repairCost = 80m + rng.Next(0, 240);
                var workItem = new WorkItem
                {
                    Assessment = assessment,
                    BodyPanel = panel,
                    Description = panel.DescriptionGr,
                    Cost_Paint = paintCost,
                    Cost_Repair = repairCost,
                    Total = paintCost + repairCost,
                    CreatedAt = createdAt.AddDays(2),
                    UpdatedAt = createdAt.AddDays(2)
                };
                assessment.WorkItems.Add(workItem);
                assessment.LaborCost += workItem.Total;
            }
            assessment.PartsCost = stageNum >= (int)InsuranceCaseStatus.PartsApprovalAndOrder ? 250m + rng.Next(0, 800) : null;
            assessment.TotalEstimatedCost = assessment.LaborCost + (assessment.PartsCost ?? 0m) + (assessment.PaintMaterialsCost ?? 0m);
            assessment.AgreedAmount = assessment.TotalEstimatedCost;
            case_.Assessment = assessment;
            _db.Assessments.Add(assessment);
        }

        // From InsuranceApproval (4): approval row. Pending at stage 4, Approved at 5+.
        if (stageNum >= (int)InsuranceCaseStatus.InsuranceApproval)
        {
            var approvalStatus = stageNum == (int)InsuranceCaseStatus.InsuranceApproval
                ? ApprovalStatus.Pending
                : ApprovalStatus.Approved;
            var approval = new InsuranceApproval
            {
                InsuranceCase = case_,
                InsuranceCompany = insurer,
                LiabilityAccepted = approvalStatus != ApprovalStatus.Pending,
                CustomerParticipation = rng.NextDouble() < 0.3,
                ApprovedAmount = assessment?.AgreedAmount ?? 500m,
                ApprovalDate = DateOnly.FromDateTime(createdAt.AddDays(4)),
                ApprovalStatus = approvalStatus,
                CreatedAt = createdAt.AddDays(4),
                UpdatedAt = createdAt.AddDays(4)
            };
            if (approval.CustomerParticipation)
            {
                approval.ParticipationAmount = Math.Round(approval.ApprovedAmount * 0.2m, 2);
            }
            case_.Approval = approval;
            _db.InsuranceApprovals.Add(approval);
        }

        // From PartsApprovalAndOrder (6): part lines
        if (assessment is not null && stageNum >= (int)InsuranceCaseStatus.PartsApprovalAndOrder)
        {
            var receivedStatus = stageNum switch
            {
                (int)InsuranceCaseStatus.PartsApprovalAndOrder => PartReceivedStatus.Ordered,
                _ => PartReceivedStatus.Received
            };
            for (var k = 0; k < rng.Next(1, 4); k++)
            {
                var panel = panels[rng.Next(panels.Count)];
                var unit = 80m + rng.Next(0, 320);
                var qty = 1m;
                var pl = new InsurancePartLine
                {
                    Assessment = assessment,
                    DestinationBranch = branch,
                    PartType = PartType.Original,
                    PartName = panel.DescriptionGr,
                    Quantity = qty,
                    UnitCost = unit,
                    Total = unit * qty,
                    AvailabilityStatus = AvailabilityStatus.Available,
                    InsuranceApproved = true,
                    Ordered = true,
                    OrderDate = DateOnly.FromDateTime(createdAt.AddDays(6)),
                    ReceivedStatus = receivedStatus,
                    ReceivedDate = receivedStatus == PartReceivedStatus.Received
                        ? DateOnly.FromDateTime(createdAt.AddDays(8))
                        : null,
                    CreatedAt = createdAt.AddDays(6),
                    UpdatedAt = createdAt.AddDays(6)
                };
                _db.InsurancePartLines.Add(pl);
            }
        }

        // From RepairScheduling (7): repair row
        if (stageNum >= (int)InsuranceCaseStatus.RepairScheduling)
        {
            var repairStatus = stageNum switch
            {
                (int)InsuranceCaseStatus.RepairScheduling => RepairStatus.Scheduled,
                (int)InsuranceCaseStatus.RepairInProgress => RepairStatus.InProgress,
                _ => RepairStatus.Completed
            };
            var scheduled = createdAt.AddDays(10);
            var repair = new Repair
            {
                InsuranceCase = case_,
                ScheduledDate = DateOnly.FromDateTime(scheduled),
                ScheduledTime = new TimeOnly(9 + rng.Next(0, 8), 0),
                Status = repairStatus,
                StartDate = repairStatus >= RepairStatus.InProgress ? scheduled : null,
                CompletionDate = repairStatus == RepairStatus.Completed ? scheduled.AddDays(2) : null,
                Notes = "Demo repair",
                CreatedAt = createdAt.AddDays(9),
                UpdatedAt = createdAt.AddDays(9)
            };
            case_.Repair = repair;
            _db.Repairs.Add(repair);
        }

        // From Settlement (10): quote row
        if (assessment is not null && stageNum >= (int)InsuranceCaseStatus.Settlement)
        {
            var subtotal = assessment.TotalEstimatedCost;
            var vatRate = 0.24m;
            var vatAmount = Math.Round(subtotal * vatRate, 2);
            var quote = new Quote
            {
                InsuranceCase = case_,
                QuoteNumber = $"Q-{case_.CaseNumber}",
                IssueDate = DateOnly.FromDateTime(createdAt.AddDays(14)),
                ResponsibleUserId = responsibleUserId,
                LaborSubtotal = assessment.LaborCost,
                PartsSubtotal = assessment.PartsCost ?? 0m,
                Subtotal = subtotal,
                VatRate = vatRate,
                VatAmount = vatAmount,
                Total = subtotal + vatAmount,
                IsCurrent = true,
                CreatedAt = createdAt.AddDays(14),
                UpdatedAt = createdAt.AddDays(14)
            };
            case_.Quotes.Add(quote);
            _db.Quotes.Add(quote);
        }

        // From PaymentConfirmed (11): payment(s)
        if (stageNum >= (int)InsuranceCaseStatus.PaymentConfirmed && case_.Approval is not null)
        {
            var amount = case_.Approval.ApprovedAmount;
            var payment = new Payment
            {
                InsuranceCase = case_,
                Amount = amount,
                PaymentDate = DateOnly.FromDateTime(createdAt.AddDays(20)),
                PaymentMethod = PaymentMethod.InsurancePayout,
                Payer = insurer.Name,
                ReferenceNumber = $"PAY-{rng.Next(100000, 999999)}",
                CreatedAt = createdAt.AddDays(20),
                UpdatedAt = createdAt.AddDays(20)
            };
            case_.Payments.Add(payment);
            _db.Payments.Add(payment);
        }

        // From CaseClosed (12): closed timestamp
        if (stageNum >= (int)InsuranceCaseStatus.CaseClosed)
        {
            case_.ClosedAt = createdAt.AddDays(22);
            case_.UpdatedAt = case_.ClosedAt.Value;
        }
    }

    private void BuildRetailCaseAtStage(
        RetailCaseStatus targetStage,
        string caseNumber,
        Customer customer,
        Vehicle vehicle,
        Branch branch,
        Random rng)
    {
        var createdAt = RandomPastDate(rng, daysBack: 60);
        var finalCost = 150m + rng.Next(0, 2400);
        var vatRate = 0.24m;
        var vat = Math.Round(finalCost * vatRate, 2);
        var workTypes = new[] { "Polish", "Paint touch-up", "Dent removal", "Bumper repair", "Full detail" };
        var case_ = new RetailCase
        {
            CaseNumber = caseNumber,
            Customer = customer,
            Vehicle = vehicle,
            Branch = branch,
            Status = targetStage,
            WorkType = workTypes[rng.Next(workTypes.Length)],
            FinalCost = finalCost,
            VatAmount = vat,
            TotalWithVat = finalCost + vat,
            ScheduledDate = DateOnly.FromDateTime(createdAt.AddDays(rng.Next(1, 5))),
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
        _db.RetailCases.Add(case_);

        var stageNum = (int)targetStage;

        // From InProgress (3): repair row + a couple of part lines
        if (stageNum >= (int)RetailCaseStatus.InProgress)
        {
            var repairStatus = stageNum switch
            {
                (int)RetailCaseStatus.InProgress => RepairStatus.InProgress,
                _ => RepairStatus.Completed
            };
            var sched = createdAt.AddDays(2);
            var repair = new RetailRepair
            {
                RetailCase = case_,
                ScheduledDate = DateOnly.FromDateTime(sched),
                ScheduledTime = new TimeOnly(10 + rng.Next(0, 6), 0),
                Status = repairStatus,
                StartDate = sched,
                CompletionDate = repairStatus == RepairStatus.Completed ? sched.AddDays(1) : null,
                CreatedAt = createdAt.AddDays(1),
                UpdatedAt = createdAt.AddDays(1)
            };
            case_.Repair = repair;
            _db.RetailRepairs.Add(repair);

            for (var k = 0; k < rng.Next(1, 3); k++)
            {
                var unit = 30m + rng.Next(0, 250);
                var pl = new RetailPartLine
                {
                    RetailCase = case_,
                    DestinationBranch = branch,
                    PartType = PartType.Original,
                    PartName = "Demo retail part",
                    Quantity = 1m,
                    UnitCost = unit,
                    Total = unit,
                    ReceivedStatus = PartReceivedStatus.Received,
                    CreatedAt = createdAt.AddDays(1),
                    UpdatedAt = createdAt.AddDays(1)
                };
                _db.RetailPartLines.Add(pl);
            }
        }

        // From Completed (4): CompletedAt timestamp
        if (stageNum >= (int)RetailCaseStatus.Completed)
        {
            case_.CompletedAt = createdAt.AddDays(4);
            case_.UpdatedAt = case_.CompletedAt.Value;
        }

        // From Paid (5): payment row
        if (stageNum >= (int)RetailCaseStatus.Paid)
        {
            var methods = new[] { PaymentMethod.Card, PaymentMethod.Cash, PaymentMethod.BankTransfer };
            var payment = new Payment
            {
                RetailCase = case_,
                Amount = case_.TotalWithVat,
                PaymentDate = DateOnly.FromDateTime(createdAt.AddDays(5)),
                PaymentMethod = methods[rng.Next(methods.Length)],
                Payer = customer.LastName ?? customer.CompanyName ?? "Customer",
                CreatedAt = createdAt.AddDays(5),
                UpdatedAt = createdAt.AddDays(5)
            };
            case_.Payments.Add(payment);
            _db.Payments.Add(payment);
        }
    }

    private async Task<List<Customer>> EnsureDemoCustomerPoolAsync(int count, Random rng, CancellationToken ct)
    {
        // Reuse the demo Customer created earlier (customer@demo.local) and add (count-1) more.
        var existing = await _db.Customers.OrderBy(c => c.MobilePhone).ToListAsync(ct);
        var list = new List<Customer>(existing);
        var firstNames = new[] { "Maria", "Giorgos", "Eleni", "Dimitris", "Katerina", "Nikos", "Sofia", "Vasilis", "Ioanna", "Kostas", "Anna", "Panagiotis" };
        var lastNames = new[] { "Papadopoulos", "Nikolaou", "Georgiou", "Ioannou", "Dimitriou", "Konstantinou", "Petrou", "Antoniou", "Markou", "Vlachos", "Stavrou", "Hatzis" };
        var phoneSeed = 6910000000L;
        var idx = 0;
        while (list.Count < count)
        {
            var first = firstNames[idx % firstNames.Length];
            var last = lastNames[idx % lastNames.Length];
            var phone = (phoneSeed + idx).ToString();
            if (list.Any(c => c.MobilePhone == phone)) { idx++; continue; }
            var c = new Customer
            {
                CustomerType = CustomerType.Individual,
                FirstName = first,
                LastName = last,
                MobilePhone = phone,
                Email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@demo.local",
                AddressLine = $"Odos {last} {rng.Next(1, 200)}",
                City = "Athens",
                PostalCode = $"1{rng.Next(0, 9)}{rng.Next(0, 9)}{rng.Next(0, 9)}{rng.Next(0, 9)}",
                GdprConsent = true,
                GdprConsentAt = DateTime.UtcNow,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Customers.Add(c);
            list.Add(c);
            idx++;
        }
        await _db.SaveChangesAsync(ct);
        return list;
    }

    private async Task<List<Vehicle>> EnsureDemoVehiclePoolAsync(
        IReadOnlyList<Customer> customers, IReadOnlyList<InsuranceCompany> insurers, Random rng, CancellationToken ct)
    {
        var existing = await _db.Vehicles.OrderBy(v => v.PlateNumber).ToListAsync(ct);
        var list = new List<Vehicle>(existing);
        var brands = new[] { ("Toyota", "Yaris"), ("Volkswagen", "Polo"), ("Opel", "Corsa"), ("Fiat", "500"),
                             ("Ford", "Fiesta"), ("Hyundai", "i20"), ("Peugeot", "208"), ("Skoda", "Fabia"),
                             ("Citroen", "C3"), ("Nissan", "Micra"), ("Renault", "Clio"), ("Mazda", "2") };
        var colors = new[] { "White", "Black", "Silver", "Red", "Blue", "Grey" };
        var i = 0;
        foreach (var customer in customers)
        {
            if (list.Any(v => v.CustomerId == customer.Id || v.Customer == customer)) continue;
            var (brand, model) = brands[i % brands.Length];
            var plate = $"{(char)('A' + (i % 26))}{(char)('B' + (i % 25))}{(char)('C' + (i % 24))}-{1000 + i}";
            var v = new Vehicle
            {
                Customer = customer,
                PlateNumber = plate,
                Brand = brand,
                Model = model,
                Year = 2015 + rng.Next(0, 10),
                Color = colors[rng.Next(colors.Length)],
                InsuranceCompany = insurers[rng.Next(insurers.Count)],
                PolicyNumber = $"POL-{rng.Next(100000, 999999)}",
                InsuranceExpiration = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(rng.Next(1, 24))),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Vehicles.Add(v);
            list.Add(v);
            i++;
        }
        await _db.SaveChangesAsync(ct);
        return list;
    }

    // Idempotent backfill: ensures each demo customer has 2-3 vehicles so the
    // /vehicles list shows realistic variety. Existing cases keep their original
    // vehicle FK — we only add, never reassign.
    private async Task ExpandDemoVehicleFleetAsync(CancellationToken ct)
    {
        var demoCustomers = await _db.Customers
            .Where(c => c.Email != null && c.Email.EndsWith("@demo.local"))
            .ToListAsync(ct);
        if (demoCustomers.Count == 0) return;

        var customerIds = demoCustomers.Select(c => c.Id).ToHashSet();
        var vehicleCounts = await _db.Vehicles
            .Where(v => customerIds.Contains(v.CustomerId))
            .GroupBy(v => v.CustomerId)
            .Select(g => new { CustomerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Count, ct);

        var existingPlates = await _db.Vehicles.Select(v => v.PlateNumber).ToListAsync(ct);
        var plateSet = new HashSet<string>(existingPlates, StringComparer.OrdinalIgnoreCase);
        var insurers = await _db.InsuranceCompanies.OrderBy(c => c.Name).ToListAsync(ct);

        var brands = new[] {
            ("Toyota", "Corolla"), ("Volkswagen", "Golf"), ("Opel", "Astra"), ("Fiat", "Tipo"),
            ("Ford", "Focus"), ("Hyundai", "i30"), ("Peugeot", "308"), ("Skoda", "Octavia"),
            ("Citroen", "C4"), ("Nissan", "Qashqai"), ("Renault", "Megane"), ("Mazda", "3"),
            ("Honda", "Civic"), ("Mitsubishi", "Lancer"), ("Suzuki", "Swift"), ("Kia", "Ceed")
        };
        var colors = new[] { "White", "Black", "Silver", "Red", "Blue", "Grey", "Green" };

        var rng = new Random(7);
        var added = 0;
        foreach (var customer in demoCustomers)
        {
            var have = vehicleCounts.GetValueOrDefault(customer.Id, 0);
            var target = rng.Next(2, 4); // 2 or 3
            for (var k = have; k < target; k++)
            {
                var (brand, model) = brands[rng.Next(brands.Length)];
                string plate;
                var attempt = 0;
                do
                {
                    plate = $"{RandomLetter(rng)}{RandomLetter(rng)}{RandomLetter(rng)}-{rng.Next(1000, 9999)}";
                    attempt++;
                } while (!plateSet.Add(plate) && attempt < 50);
                if (attempt >= 50) break;

                _db.Vehicles.Add(new Vehicle
                {
                    CustomerId = customer.Id,
                    PlateNumber = plate,
                    Brand = brand,
                    Model = model,
                    Year = 2015 + rng.Next(0, 10),
                    Color = colors[rng.Next(colors.Length)],
                    InsuranceCompanyId = insurers.Count > 0 ? insurers[rng.Next(insurers.Count)].Id : null,
                    PolicyNumber = $"POL-{rng.Next(100000, 999999)}",
                    InsuranceExpiration = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(rng.Next(1, 24))),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                added++;
            }
        }
        if (added > 0)
        {
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Expanded demo vehicle fleet by {n} vehicles.", added);
        }
    }

    private static char RandomLetter(Random rng) => (char)('A' + rng.Next(0, 26));

    // Existing demos were seeded against a single branch; if a 2nd branch has since
    // appeared (e.g. added via seed file or admin UI), spread the demo cases so the
    // branch selector + breakdown actually shows variation. Real (non-demo) cases
    // are left untouched.
    private async Task RebalanceDemoCasesAcrossBranchesAsync(CancellationToken ct)
    {
        var branches = await _db.Branches.Where(b => b.IsActive).OrderBy(b => b.Code).ToListAsync(ct);
        if (branches.Count < 2) return;

        var insurance = await _db.InsuranceCases
            .Where(c => c.CaseNumber.StartsWith("INS-DEMO-"))
            .OrderBy(c => c.CaseNumber).ToListAsync(ct);
        var retail = await _db.RetailCases
            .Where(c => c.CaseNumber.StartsWith("RET-DEMO-"))
            .OrderBy(c => c.CaseNumber).ToListAsync(ct);
        if (insurance.Count == 0 && retail.Count == 0) return;

        var distinct = insurance.Select(c => c.BranchId)
            .Concat(retail.Select(c => c.BranchId))
            .Distinct().Count();
        if (distinct >= branches.Count) return; // already spread

        var rebalanced = 0;
        for (var i = 0; i < insurance.Count; i++)
        {
            var target = branches[i % branches.Count].Id;
            if (insurance[i].BranchId != target) { insurance[i].BranchId = target; rebalanced++; }
        }
        for (var i = 0; i < retail.Count; i++)
        {
            var target = branches[i % branches.Count].Id;
            if (retail[i].BranchId != target) { retail[i].BranchId = target; rebalanced++; }
        }
        if (rebalanced > 0)
        {
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Rebalanced {n} demo cases across {b} branches.", rebalanced, branches.Count);
        }
    }

    // Demo cases set Status directly without recording the transitions, so the
    // Timeline tab is empty. Walk each case from stage 1 → its current stage and
    // synthesize CaseEvent rows. Idempotent: skips cases that already have events.
    private async Task BackfillDemoCaseEventsAsync(CancellationToken ct)
    {
        var admin = await _db.Users.FirstOrDefaultAsync(u => u.Email == "admin@paintbull.local", ct);
        if (admin is null) return;

        // Insurance side
        var insuranceCases = await _db.InsuranceCases
            .Where(c => c.CaseNumber.StartsWith("INS-DEMO-"))
            .ToListAsync(ct);
        var insuranceWithEvents = await _db.CaseEvents
            .Where(e => e.InsuranceCaseId != null
                        && insuranceCases.Select(c => c.Id).Contains(e.InsuranceCaseId!.Value))
            .Select(e => e.InsuranceCaseId!.Value).Distinct().ToListAsync(ct);
        var insuranceMissing = insuranceCases.Where(c => !insuranceWithEvents.Contains(c.Id)).ToList();

        var insuranceStages = Enum.GetValues<InsuranceCaseStatus>()
            .OrderBy(s => (int)s).ToArray();
        var added = 0;
        foreach (var c in insuranceMissing)
        {
            var endIndex = Array.IndexOf(insuranceStages, c.Status);
            if (endIndex < 0) continue;
            var span = TimeSpan.FromTicks(((c.ClosedAt ?? c.UpdatedAt) - c.CreatedAt).Ticks);
            var stepCount = endIndex + 1; // events from 1st stage through current
            for (var step = 0; step < stepCount; step++)
            {
                var fraction = stepCount == 1 ? 0.0 : (double)step / (stepCount - 1);
                var occurredAt = c.CreatedAt.AddTicks((long)(span.Ticks * fraction));
                _db.CaseEvents.Add(new CaseEvent
                {
                    InsuranceCaseId = c.Id,
                    FromStatus = step == 0 ? null : insuranceStages[step - 1].ToString(),
                    ToStatus = insuranceStages[step].ToString(),
                    TriggeredById = admin.Id,
                    OccurredAt = occurredAt,
                    Reason = "Backfilled by demo seeder",
                    CreatedAt = occurredAt,
                    UpdatedAt = occurredAt
                });
                added++;
            }
        }

        // Retail side
        var retailCases = await _db.RetailCases
            .Where(c => c.CaseNumber.StartsWith("RET-DEMO-"))
            .ToListAsync(ct);
        var retailWithEvents = await _db.CaseEvents
            .Where(e => e.RetailCaseId != null
                        && retailCases.Select(c => c.Id).Contains(e.RetailCaseId!.Value))
            .Select(e => e.RetailCaseId!.Value).Distinct().ToListAsync(ct);
        var retailMissing = retailCases.Where(c => !retailWithEvents.Contains(c.Id)).ToList();

        var retailStages = Enum.GetValues<RetailCaseStatus>()
            .OrderBy(s => (int)s).ToArray();
        foreach (var c in retailMissing)
        {
            var endIndex = Array.IndexOf(retailStages, c.Status);
            if (endIndex < 0) continue;
            var span = TimeSpan.FromTicks(((c.CompletedAt ?? c.UpdatedAt) - c.CreatedAt).Ticks);
            var stepCount = endIndex + 1;
            for (var step = 0; step < stepCount; step++)
            {
                var fraction = stepCount == 1 ? 0.0 : (double)step / (stepCount - 1);
                var occurredAt = c.CreatedAt.AddTicks((long)(span.Ticks * fraction));
                _db.CaseEvents.Add(new CaseEvent
                {
                    RetailCaseId = c.Id,
                    FromStatus = step == 0 ? null : retailStages[step - 1].ToString(),
                    ToStatus = retailStages[step].ToString(),
                    TriggeredById = admin.Id,
                    OccurredAt = occurredAt,
                    Reason = "Backfilled by demo seeder",
                    CreatedAt = occurredAt,
                    UpdatedAt = occurredAt
                });
                added++;
            }
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Backfilled {n} CaseEvent rows for demo cases.", added);
        }
    }

    private static T PickFrom<T>(IReadOnlyList<T> source, Random rng) => source[rng.Next(source.Count)];

    private static DateTime RandomPastDate(Random rng, int daysBack)
    {
        var now = DateTime.UtcNow;
        var days = rng.Next(0, daysBack);
        var hours = rng.Next(0, 24);
        return now.AddDays(-days).AddHours(-hours);
    }
}
