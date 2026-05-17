# Workshop — Domain Model

**Project:** Paint Bull workshop management platform
**Owner:** Paint Bull (Παν. Τσαλδάρη 41, Ταύρος, Athens · 2102202898)
**Purpose:** Replace Excel-based workflow with a multi-user, multi-branch web platform that handles both insurance repair jobs and retail jobs.

This document is the single source of truth for the data model and behavior. All scaffolding, migrations, and code generation work against the entities, enumerations, and state-machine rules defined here.

---

## 1. Tech stack (locked)

| Layer | Choice | Notes |
|---|---|---|
| Backend | ASP.NET Core 10 | C# 13 |
| Frontend | Blazor Server | Staff app plus customer / insurer / supplier portals (all served by `Workshop.Web`) |
| UI library | **MudBlazor** | Material Design 3 components, themable |
| ORM | EF Core 10 | Code-first migrations, snake-case naming convention |
| Database | PostgreSQL 16 | via Npgsql provider |
| Auth | ASP.NET Core Identity | 4 user types (`PortalAudience`) via roles + claims |
| Workflow engine | Stateless 5.20.x | State machine for InsuranceCase 12 stages |
| Validation | FluentValidation 12 | Request/command validation, MediatR pipeline behavior |
| PDF | QuestPDF 2026.x | Quote PDF in place; Invoice / Receipt / Case Form templates pending |
| i18n | IStringLocalizer + .resx | EL primary, EN secondary (401 keys each, at parity) |
| File storage | Local filesystem (dev) | Behind an `IFileStore` abstraction; S3/Azure Blob adapter pending |
| Notifications | Email + SMS + in-app | Logging-only stub senders in place behind `IEmailSender` / `ISmsSender` |
| myDATA (AADE) | Stub client | `IMyDataClient` with deterministic fake MARK; real AADE REST adapter pending |
| Logging | Serilog → file + console | Structured logs |
| Background jobs | Hangfire (planned) | Listed in stack, not yet wired — needed for reminders + scheduled myDATA submission |

---

## 2. Solution layout

Single Blazor Server app serves all four audiences. No per-portal SPA, no shared REST API. The original four-project split was discarded once it became clear Blazor Server pages would call MediatR handlers directly — see §8 "Resolved" item on portal split.

```
CARWorshoNew/
├── DOMAIN-MODEL.md                  # ← this file
├── src/
│   ├── Workshop.Domain/             # Entities, enums, value objects, workflow state machine
│   ├── Workshop.Application/        # Use cases, DTOs, validators, notification + myDATA abstractions
│   ├── Workshop.Infrastructure/     # EF Core DbContext, migrations, file storage, stub adapters
│   └── Workshop.Web/                # Blazor Server — staff + customer + insurer + supplier UIs
├── tests/
│   ├── Workshop.Domain.Tests/       # Workflow state machine tests
│   └── Workshop.Application.Tests/  # Use case tests (EF InMemory)
├── seed/
│   ├── body-panels.json             # 78 panels from ΜΕΡΗ ΑΥΤΟΚΙΝΗΤΟΥ2.xlsx
│   ├── body-panel-operations.json   # Allowed-ops matrix
│   └── insurance-companies.json     # Greek insurance companies (seed list)
└── docker-compose.yml               # PostgreSQL + pgAdmin for local dev
```

---

## 3. Enumerations

### 3.1 OperationType (work item categories)
Source: union of `ΜΕΡΗ ΑΥΤΟΚΙΝΗΤΟΥ2.xlsx` matrix columns + image 2 work-items grid.

| Code | Ελληνικά | English |
|---|---|---|
| `Polish` | ΓΥΑΛΙΣΜΑ | Polish |
| `PDR` | PDR | Paintless Dent Repair |
| `RemoveRefit` | ΕΞΑΓΩΓΗ-ΕΠΑΝΑΤΟΠΟΘΕΤΗΣΗ | Remove & Refit |
| `Replace` | ΑΝΤΙΚΑΤΑΣΤΑΣΗ | Replace |
| `DisassembleAssemble` | ΑΠΟΣ.-ΣΥΝ. | Disassemble & Assemble |
| `Repair` | ΕΠΙΣΚΕΥΗ | Repair |
| `Paint` | ΒΑΦΗ | Paint |
| `RepairPaint` | ΕΠΙΣΚΕΥΗ+ΒΑΦΗ | Repair + Paint (combo, priced distinctly) |
| `Weld` | ΣΥΓΚΟΛΛΗΣΗ | Welding |
| `Other` | ΑΛΛΟ | Other |

### 3.2 PartType (per part line)
Source: image 4 (parts grid).

| Code | Ελληνικά | English |
|---|---|---|
| `Original` | ΓΝΗΣΙΟ | OEM / Original |
| `NonOEM` | ΟΧΙ ΓΝΗΣΙΟ | Aftermarket |
| `MTX` | ΜΕΤΑΧΕΙΡΙΣΜΕΝΟ | Used / Secondhand |
| `Other` | ΑΛΛΟ | Other |

### 3.3 InsuranceCaseStatus (12-stage workflow)
| Code | Ελληνικά |
|---|---|
| `NewCase` | Νέος Φάκελος |
| `AssessorAppointment` | Ραντεβού Πραγματογνώμονα |
| `Assessment` | Πραγματογνωμοσύνη |
| `InsuranceApproval` | Έγκριση Ασφαλιστικής |
| `CustomerAssignment` | Εκχώρηση Πελάτη |
| `PartsApprovalAndOrder` | Έγκριση & Παραγγελία Ανταλλακτικών |
| `RepairScheduling` | Προγραμματισμός Επισκευής |
| `RepairInProgress` | Επισκευή σε Εξέλιξη |
| `RepairCompleted` | Ολοκλήρωση Επισκευής |
| `Settlement` | Εξοφλητική |
| `PaymentConfirmed` | Επιβεβαίωση Πληρωμής |
| `CaseClosed` | Κλείσιμο Φακέλου |

### 3.4 RetailCaseStatus (simplified)
| Code | Ελληνικά |
|---|---|
| `Quoted` | Προσφορά |
| `Accepted` | Αποδοχή |
| `InProgress` | Σε Εξέλιξη |
| `Completed` | Ολοκληρώθηκε |
| `Paid` | Εξοφλήθηκε |
| `Closed` | Έκλεισε |

### 3.5 PartReceivedStatus (multi-state, replaces the docx's boolean)
| Code | Ελληνικά | Notes |
|---|---|---|
| `Pending` | Εκκρεμεί | Default — not yet ordered |
| `Ordered` | Παραγγέλθηκε | Order placed with supplier |
| `InTransit` | Σε Μεταφορά | Confirmed by supplier, en route |
| `Received` | Παραλήφθηκε | In our warehouse |
| `Defective` | Ελαττωματικό | Received but unusable, awaiting replacement |
| `Cancelled` | Ακυρώθηκε | Order cancelled |

### 3.6 AvailabilityStatus
| Code | Ελληνικά |
|---|---|
| `Available` | Διαθέσιμο |
| `OutOfStock` | Εξαντλημένο |
| `Discontinued` | Καταργημένο |
| `Unknown` | Άγνωστο |

### 3.7 DocumentType
| Code | Ελληνικά |
|---|---|
| `CaseForm` | Έντυπο Φακέλου |
| `InsuranceForm` | Έντυπο Ασφαλιστικής |
| `Invoice` | Τιμολόγιο |
| `Receipt` | Απόδειξη |
| `Quote` | Προσφορά |
| `IdCopy` | Αντίγραφο Ταυτότητας |
| `VehicleLicense` | Άδεια Κυκλοφορίας |
| `Other` | Άλλο |

### 3.8 CustomerType
| Code | Ελληνικά |
|---|---|
| `Individual` | Ιδιώτης |
| `Company` | Εταιρεία |

### 3.9 PortalAudience (for User entity)
| Code | Purpose |
|---|---|
| `Staff` | Internal Paint Bull employees |
| `Customer` | End customers |
| `Insurance` | External insurance company reviewers |
| `Supplier` | External parts suppliers |

### 3.10 StaffRole (within `PortalAudience.Staff`)
| Code | Ελληνικά |
|---|---|
| `Admin` | Διαχειριστής |
| `BranchManager` | Υπεύθυνος Καταστήματος |
| `Receptionist` | Υπάλληλος Υποδοχής |
| `Technician` | Τεχνικός |
| `BodyShopManager` | Υπεύθυνος Φανοποιείου |

---

## 4. Entities

> **Convention:** every entity has `Id` (Guid), `CreatedAt` (DateTime), `UpdatedAt` (DateTime), `CreatedById` (Guid FK→User), `UpdatedById` (Guid FK→User), and soft-delete flag `IsDeleted` (bool). These cross-cutting fields are omitted from each table below to reduce noise.

### 4.1 Shared / Reference

#### CompanyProfile (single-row table)
| Field | Type | Required | Note |
|---|---|---|---|
| `Name` | string(200) | Y | "Paint Bull" |
| `AddressLine` | string(300) | Y | |
| `City` | string(100) | Y | |
| `PostalCode` | string(10) | N | |
| `Phone` | string(30) | Y | |
| `Email` | string(200) | N | |
| `VatNumber` (ΑΦΜ) | string(20) | Y | |
| `TaxOffice` (ΔΟΥ) | string(100) | N | |
| `LogoPath` | string(500) | N | Used on PDF quotes |
| `DefaultVatRate` | decimal(5,2) | Y | 24.00 |

#### Branch (Κατάστημα)
| Field | Type | Required |
|---|---|---|
| `Name` | string(200) | Y |
| `Code` | string(20) | Y, unique |
| `AddressLine` | string(300) | Y |
| `City` | string(100) | Y |
| `PostalCode` | string(10) | N |
| `Phone` | string(30) | N |
| `IsActive` | bool | Y |

#### Warehouse (1:1 with Branch)
| Field | Type | Required |
|---|---|---|
| `BranchId` | Guid FK→Branch | Y, unique |
| `Name` | string(200) | Y |
| `Description` | text | N |

#### Customer (Πελάτης) — individual or company
| Field | Type | Required | Note |
|---|---|---|---|
| `CustomerType` | enum CustomerType | Y | Individual / Company |
| `FirstName` (Όνομα) | string(100) | Conditional | Required if Individual |
| `LastName` (Επώνυμο) | string(100) | Conditional | Required if Individual |
| `CompanyName` (Επωνυμία) | string(300) | Conditional | Required if Company |
| `VatNumber` (ΑΦΜ) | string(20) | N | |
| `TaxOffice` (ΔΟΥ) | string(100) | N | |
| `IdNumber` (Ταυτότητα) | string(30) | N | |
| `MobilePhone` (Κινητό) | string(30) | Y | |
| `SecondaryPhone` | string(30) | N | |
| `Email` | string(200) | N | |
| `AddressLine` | string(300) | N | |
| `City` | string(100) | N | |
| `PostalCode` | string(10) | N | |
| `GdprConsent` | bool | Y | |
| `GdprConsentAt` | DateTime | Conditional | Required when GdprConsent=true |
| `Notes` | text | N | |
| `IsActive` | bool | Y | |

#### Vehicle (Όχημα)
| Field | Type | Required |
|---|---|---|
| `CustomerId` | Guid FK→Customer | Y |
| `PlateNumber` (Αρ. Κυκλ.) | string(20) | Y, indexed |
| `Vin` | string(30) | N |
| `Brand` (Μάρκα) | string(100) | Y |
| `Model` (Μοντέλο) | string(100) | Y |
| `Version` (Έκδοση) | string(100) | N |
| `Year` (Έτος) | int | N |
| `Color` (Χρώμα) | string(50) | N |
| `FuelType` | enum FuelType | N |
| `Mileage` (Χλμ) | int | N |
| `InsuranceCompanyId` | Guid FK→InsuranceCompany | N |
| `PolicyNumber` (Αρ. Συμβ.) | string(50) | N |
| `InsuranceExpiration` (Λήξη Ασφ.) | date | N |
| `Notes` | text | N |
| `IsActive` | bool | Y |

#### InsuranceCompany (Ασφαλιστική)
| Field | Type | Required |
|---|---|---|
| `Name` | string(200) | Y, unique |
| `VatNumber` | string(20) | N |
| `Phone` | string(30) | N |
| `Email` | string(200) | N |
| `AddressLine` | string(300) | N |
| `Notes` | text | N |
| `IsActive` | bool | Y |

#### Assessor (Πραγματογνώμονας) — external insurance role
| Field | Type | Required |
|---|---|---|
| `FullName` | string(200) | Y |
| `Phone` | string(30) | N |
| `Email` | string(200) | N |
| `LicenseNumber` | string(50) | N |
| `InsuranceCompanyId` | Guid FK→InsuranceCompany | N (may be independent) |
| `Notes` | text | N |
| `IsActive` | bool | Y |

#### Adjuster (Διακανονιστής) — external insurance settlement role
| Field | Type | Required |
|---|---|---|
| `FullName` | string(200) | Y |
| `Phone` | string(30) | N |
| `Email` | string(200) | N |
| `InsuranceCompanyId` | Guid FK→InsuranceCompany | N |
| `Notes` | text | N |
| `IsActive` | bool | Y |

#### Supplier (Προμηθευτής)
| Field | Type | Required |
|---|---|---|
| `Name` | string(200) | Y |
| `VatNumber` | string(20) | N |
| `Phone` | string(30) | N |
| `Email` | string(200) | N |
| `AddressLine` | string(300) | N |
| `ContactPerson` | string(200) | N |
| `Notes` | text | N |
| `IsActive` | bool | Y |

#### BodyPanel (catalog, seeded from xlsx)
| Field | Type | Required | Note |
|---|---|---|---|
| `Code` | string(10) | Y, unique | "1", "21Α", "34Δ", etc. (matches diagram numbering) |
| `DescriptionGr` | string(200) | Y | "ΠΡΟΦΥΛΑΚΤΗΡΑΣ ΕΜΠΡΟΣ" |
| `DescriptionEn` | string(200) | N | "Front bumper" |
| `Category` | enum (External / Internal / Safety) | Y | |
| `Side` | enum (Center / Left / Right) | Y | |
| `DiagramX` | decimal(5,2) | N | Pixel coords on car diagram for click targets |
| `DiagramY` | decimal(5,2) | N | |
| `IsActive` | bool | Y | |

#### BodyPanelOperation (allowed-ops junction, seeded from xlsx)
| Field | Type | Required |
|---|---|---|
| `BodyPanelId` | Guid FK→BodyPanel | Y |
| `Operation` | enum OperationType | Y |
| _PK is (BodyPanelId, Operation)_ | | |

#### ServiceCatalog (the car diagram's outer labels)
| Field | Type | Required |
|---|---|---|
| `Code` | string(50) | Y, unique |
| `NameGr` | string(200) | Y |
| `NameEn` | string(200) | N |
| `DefaultPrice` | decimal(10,2) | N |
| `Description` | text | N |
| `IsActive` | bool | Y |

Seed values: Sun Films (Αντηλιακές Μεμβράνες), Polish (Γυάλισμα), Detailing (Βιολογικός Καθαρισμός), External Repairs (Επισκευές Εξωτερικά), Window Regulators (Γρύλοι Παραθύρων).

---

### 4.2 Identity (ASP.NET Core Identity, extended)

#### User
Extends `IdentityUser<Guid>`. Adds:
| Field | Type | Required | Note |
|---|---|---|---|
| `FullName` | string(200) | Y | |
| `PortalAudience` | enum PortalAudience | Y | Staff / Customer / Insurance / Supplier |
| `BranchId` | Guid FK→Branch | Conditional | Required for Staff |
| `CustomerId` | Guid FK→Customer | Conditional | Required for Customer audience |
| `InsuranceCompanyId` | Guid FK→InsuranceCompany | Conditional | Required for Insurance audience |
| `SupplierId` | Guid FK→Supplier | Conditional | Required for Supplier audience |
| `Language` | string(5) | Y | "el" / "en", default "el" |
| `IsActive` | bool | Y | |

Roles (Identity roles): `Admin`, `BranchManager`, `Receptionist`, `Technician`, `BodyShopManager`.

---

### 4.3 Insurance flow

#### InsuranceCase (Φάκελος Ασφαλιστικής)
| Field | Type | Required | Note |
|---|---|---|---|
| `CaseNumber` | string(30) | Y, unique | Generated, e.g., "INS-2026-0001" |
| `CustomerId` | Guid FK→Customer | Y | Vehicle owner |
| `VehicleId` | Guid FK→Vehicle | Y | |
| `BranchId` | Guid FK→Branch | Y | Repair branch |
| `InsuranceCompanyId` | Guid FK→InsuranceCompany | Y | |
| `ClaimNumber` (Αρ. Ζημιάς) | string(50) | N | |
| `Status` | enum InsuranceCaseStatus | Y | Driven by state machine |
| `Priority` | enum (Low / Normal / High / Urgent) | N | |
| `AssignedUserId` | Guid FK→User | N | Staff member responsible |
| `AssessorId` | Guid FK→Assessor | N | |
| `AdjusterId` | Guid FK→Adjuster | N | |
| `DriverFirstName` | string(100) | N | If different from owner |
| `DriverLastName` | string(100) | N | |
| `DriverPhone` | string(30) | N | |
| `DriverEmail` | string(200) | N | |
| `AccidentDate` (Ημ/νία Ατυχήματος) | date | N | |
| `MileageAtAssessment` (Χλμ) | int | N | |
| `ClosedAt` (Ημ/νία Κλεισίματος) | DateTime | N | |
| `Notes` | text | N | |

#### Assessment (Πραγματογνωμοσύνη) — 1:1 with InsuranceCase
| Field | Type | Required | Note |
|---|---|---|---|
| `InsuranceCaseId` | Guid FK→InsuranceCase | Y, unique | |
| `AssessmentDate` | date | Y | |
| `LaborCost` | decimal(10,2) | Y | Sum of WorkItems |
| `PartsRequired` | bool | Y | |
| `PartsCost` | decimal(10,2) | Conditional | Required if PartsRequired=true; sum of PartLines |
| `PaintMaterialsCost` | decimal(10,2) | N | |
| `TotalEstimatedCost` | decimal(10,2) | Y | Labor + Parts + Paint (computed but stored) |
| `AgreedAmount` (Συμφωνημένο) | decimal(10,2) | Y | |
| `AgreementDate` | date | Y | |
| `IntermediateInspection` | bool | N | |
| `Notes` | text | N | |

#### WorkItem (Εργασία) — many per Assessment, one per body panel touched
| Field | Type | Required |
|---|---|---|
| `AssessmentId` | Guid FK→Assessment | Y |
| `BodyPanelId` | Guid FK→BodyPanel | N (nullable for "Other" rows) |
| `Description` | string(300) | Y |
| `Cost_Polish` | decimal(10,2) | N |
| `Cost_PDR` | decimal(10,2) | N |
| `Cost_RemoveRefit` | decimal(10,2) | N |
| `Cost_Replace` | decimal(10,2) | N |
| `Cost_DisassembleAssemble` | decimal(10,2) | N |
| `Cost_Repair` | decimal(10,2) | N |
| `Cost_Paint` | decimal(10,2) | N |
| `Cost_RepairPaint` | decimal(10,2) | N |
| `Cost_Weld` | decimal(10,2) | N |
| `Cost_Other` | decimal(10,2) | N |
| `DiscountPct` | decimal(5,2) | N | 0–100 |
| `Total` | decimal(10,2) | Y | (sum of Cost_* columns) × (1 - DiscountPct/100), computed and stored |

> **Validation rule:** for each WorkItem, only cost columns whose `OperationType` is allowed for the linked `BodyPanel` (via `BodyPanelOperation`) may be > 0. Enforced at write time.

#### InsurancePartLine (Ανταλλακτικό Φακέλου) — many per Assessment
| Field | Type | Required | Note |
|---|---|---|---|
| `AssessmentId` | Guid FK→Assessment | Y | |
| `SupplierId` | Guid FK→Supplier | N | |
| `DestinationBranchId` (Κατάστημα Δικτύου) | Guid FK→Branch | Y | Where the part should be delivered |
| `PartType` | enum PartType | Y | Original / NonOEM / MTX / Other |
| `PartName` (Όνομα) | string(300) | Y | |
| `Quantity` (Ποσότητα) | decimal(10,2) | Y | |
| `UnitCost` (Τιμή Μονάδας) | decimal(10,2) | Y | |
| `DiscountPct` | decimal(5,2) | N | |
| `Total` | decimal(10,2) | Y | Computed |
| `AvailabilityStatus` | enum AvailabilityStatus | Y | |
| `InsuranceApproved` (Εγκρίθηκε) | bool | Y | |
| `Ordered` (Παραγγέλθηκε) | bool | Y | |
| `OrderDate` | date | N | |
| `ReceivedStatus` (Παραλήφθη) | enum PartReceivedStatus | Y | Default `Pending` |
| `ReceivedDate` | date | N | |
| `WarehouseId` | Guid FK→Warehouse | N | Filled when `ReceivedStatus = Received` |
| `StorageLocation` (Θέση Αποθήκευσης) | string(100) | N | Free text — shelf/bay |
| `Notes` | text | N | |

#### InsuranceApproval (Έγκριση Ασφαλιστικής)
| Field | Type | Required | Note |
|---|---|---|---|
| `InsuranceCaseId` | Guid FK→InsuranceCase | Y | |
| `InsuranceCompanyId` | Guid FK→InsuranceCompany | Y | |
| `LiabilityAccepted` (Υπαιτιότητα) | bool | Y | |
| `CustomerParticipation` (Συμμετοχή) | bool | Y | |
| `ParticipationAmount` | decimal(10,2) | Conditional | Required if CustomerParticipation=true |
| `ApprovedAmount` (Εγκεκριμένο) | decimal(10,2) | Y | |
| `ApprovalDate` | date | Y | |
| `ApprovalStatus` | enum (Pending / Approved / Rejected / PartialApproval) | Y | |
| `Notes` | text | N | |

#### Quote (Προσφορά) — 1:N to InsuranceCase (allow revisions)
| Field | Type | Required | Note |
|---|---|---|---|
| `InsuranceCaseId` | Guid FK→InsuranceCase | Y | |
| `QuoteNumber` | string(30) | Y, unique | "Q-2026-0001" |
| `IssueDate` | date | Y | |
| `ResponsibleUserId` | Guid FK→User | Y | The "Υπεύθυνος Προσφοράς" |
| `LaborSubtotal` | decimal(10,2) | Y | Sum of WorkItem.Total |
| `PartsSubtotal` | decimal(10,2) | Y | Sum of PartLine.Total |
| `LaborDiscountAmount` | decimal(10,2) | N | |
| `PartsDiscountAmount` | decimal(10,2) | N | |
| `Subtotal` | decimal(10,2) | Y | (Labor + Parts − Discounts) |
| `VatRate` | decimal(5,2) | Y | Default from CompanyProfile (24.00) |
| `VatAmount` | decimal(10,2) | Y | |
| `Total` | decimal(10,2) | Y | |
| `CustomerParticipation` | decimal(10,2) | N | Copy of approval value at issue time |
| `Notes` | text | N | |
| `PdfPath` | string(500) | N | Path to generated PDF |
| `IsCurrent` | bool | Y | Only one current quote per case |

#### Repair (Επισκευή) — 1:1 with InsuranceCase
| Field | Type | Required |
|---|---|---|
| `InsuranceCaseId` | Guid FK→InsuranceCase | Y, unique |
| `ScheduledDate` (Ραντεβού) | date | Y |
| `ScheduledTime` | time | N |
| `StartDate` | DateTime | N |
| `CompletionDate` | DateTime | N |
| `TechnicianId` (Τεχνικός) | Guid FK→User | N |
| `Status` | enum (Scheduled / InProgress / OnHold / Completed) | Y |
| `IntermediateInspectionDone` | bool | N |
| `Notes` | text | N |

#### Photo (Φωτογραφία)
Photos can attach to either an Assessment (damage intake photos) **or** a Repair (intermediate/completion photos). Exactly one of the two FKs is set.

| Field | Type | Required | Note |
|---|---|---|---|
| `AssessmentId` | Guid FK→Assessment | Conditional | One of (Assessment, Repair) must be set |
| `RepairId` | Guid FK→Repair | Conditional | |
| `Phase` | enum (Intake / Damage / Intermediate / Completion) | Y | |
| `FileName` | string(300) | Y | |
| `FilePath` | string(500) | Y | |
| `ContentType` | string(100) | Y | |
| `SizeBytes` | long | Y | |
| `Caption` | string(500) | N | |
| `UploadedById` | Guid FK→User | Y | |

#### Document (Έγγραφο)
| Field | Type | Required |
|---|---|---|
| `InsuranceCaseId` | Guid FK→InsuranceCase | N |
| `RetailCaseId` | Guid FK→RetailCase | N |
| `CustomerId` | Guid FK→Customer | N |
| `VehicleId` | Guid FK→Vehicle | N |
| `DocumentType` | enum DocumentType | Y |
| `FileName` | string(300) | Y |
| `FilePath` | string(500) | Y |
| `ContentType` | string(100) | Y |
| `SizeBytes` | long | Y |
| `UploadedById` | Guid FK→User | Y |
| `SentToInsurance` | bool | N |
| `SentToInsuranceAt` | DateTime | N |

> **Validation:** at least one of (InsuranceCaseId, RetailCaseId, CustomerId, VehicleId) must be set.

#### Payment (Πληρωμή)
| Field | Type | Required | Note |
|---|---|---|---|
| `InsuranceCaseId` | Guid FK→InsuranceCase | N | One of (Insurance, Retail) must be set |
| `RetailCaseId` | Guid FK→RetailCase | N | |
| `Amount` | decimal(10,2) | Y | |
| `PaymentDate` | date | Y | |
| `PaymentMethod` | enum (Cash / Card / BankTransfer / InsurancePayout / Other) | Y | |
| `Payer` | string(200) | N | "Insurance" / "Customer" / specific name |
| `ReferenceNumber` | string(100) | N | Bank ref, receipt number |
| `Notes` | text | N |

---

### 4.4 Retail flow (separate aggregates)

#### RetailCase (Φάκελος Λιανικής)
| Field | Type | Required | Note |
|---|---|---|---|
| `CaseNumber` | string(30) | Y, unique | "RET-2026-0001" |
| `CustomerId` | Guid FK→Customer | Y | |
| `VehicleId` | Guid FK→Vehicle | Y | |
| `BranchId` | Guid FK→Branch | Y | |
| `Status` | enum RetailCaseStatus | Y | |
| `AssignedUserId` | Guid FK→User | N | |
| `WorkType` | string(300) | Y | Free-text — e.g., "Φανοποιεία πλαϊνού" |
| `FinalCost` | decimal(10,2) | Y | Single total, no breakdown |
| `VatAmount` | decimal(10,2) | Y | |
| `TotalWithVat` | decimal(10,2) | Y | |
| `ScheduledDate` | date | N | |
| `CompletedAt` | DateTime | N | |
| `Notes` | text | N |

#### RetailPartLine — many per RetailCase
| Field | Type | Required |
|---|---|---|
| `RetailCaseId` | Guid FK→RetailCase | Y |
| `SupplierId` | Guid FK→Supplier | N |
| `DestinationBranchId` | Guid FK→Branch | Y |
| `PartType` | enum PartType | Y |
| `PartName` | string(300) | Y |
| `Quantity` | decimal(10,2) | Y |
| `UnitCost` | decimal(10,2) | Y |
| `Total` | decimal(10,2) | Y |
| `ReceivedStatus` | enum PartReceivedStatus | Y |
| `WarehouseId` | Guid FK→Warehouse | N |
| `StorageLocation` | string(100) | N |
| `Notes` | text | N |

#### RetailRepair — 1:1 with RetailCase (optional — only if scheduled)
| Field | Type | Required |
|---|---|---|
| `RetailCaseId` | Guid FK→RetailCase | Y, unique |
| `ScheduledDate` | date | Y |
| `ScheduledTime` | time | N |
| `StartDate` | DateTime | N |
| `CompletionDate` | DateTime | N |
| `TechnicianId` | Guid FK→User | N |
| `Status` | enum (Scheduled / InProgress / OnHold / Completed) | Y |

> _No retail-specific Photo or Document entity — those reuse the unified `Photo` and `Document` tables which can FK to either Insurance or Retail._
> **Resolved:** the `Photo` table was extended with a nullable `RetailRepairId` FK. Retail repairs use the same upload pipeline as insurance repairs.

---

### 4.5 Cross-cutting

#### CaseEvent (workflow audit trail)
| Field | Type | Required |
|---|---|---|
| `InsuranceCaseId` | Guid FK→InsuranceCase | N |
| `RetailCaseId` | Guid FK→RetailCase | N |
| `FromStatus` | string(50) | N |
| `ToStatus` | string(50) | Y |
| `TriggeredById` | Guid FK→User | Y |
| `Reason` | string(500) | N |
| `OccurredAt` | DateTime | Y |

#### AuditLog (general DB audit)
| Field | Type | Required |
|---|---|---|
| `EntityName` | string(100) | Y |
| `EntityId` | string(50) | Y |
| `Action` | enum (Created / Updated / Deleted) | Y |
| `Changes` | jsonb | N | Field-level diff |
| `UserId` | Guid FK→User | Y |
| `OccurredAt` | DateTime | Y |

#### Notification (in-app, per user)
| Field | Type | Required |
|---|---|---|
| `UserId` | Guid FK→User | Y |
| `Title` | string(200) | Y |
| `Body` | string(1000) | Y |
| `Url` | string(500) | N |
| `IsRead` | bool | Y |
| `OccurredAt` | DateTime | Y |

---

## 5. Workflow state machine — InsuranceCase

Implemented with **Stateless** in `Workshop.Domain/Workflows/InsuranceCaseStateMachine.cs`.

### 5.1 Transitions and guards

| From | Trigger | To | Guard (must be true to fire) |
|---|---|---|---|
| `NewCase` | `BookAssessorAppointment` | `AssessorAppointment` | AssessorId set, AccidentDate set, Vehicle photos uploaded (≥1) |
| `AssessorAppointment` | `CompleteAssessment` | `Assessment` | Assessment record exists with AssessmentDate, LaborCost, TotalEstimatedCost, AgreedAmount filled. At least 1 WorkItem. |
| `Assessment` | `SubmitForInsuranceApproval` | `InsuranceApproval` | Quote generated and PDF stored. Required Document types uploaded (CaseForm, InsuranceForm). |
| `InsuranceApproval` | `ApprovalReceived` | `CustomerAssignment` | InsuranceApproval record with ApprovalStatus ∈ {Approved, PartialApproval} |
| `InsuranceApproval` | `ApprovalRejected` | `NewCase` | ApprovalStatus = Rejected → reopen for revision |
| `CustomerAssignment` | `CustomerAccepts` | `PartsApprovalAndOrder` | Customer participation confirmed (if any) |
| `PartsApprovalAndOrder` | `AllPartsReceived` | `RepairScheduling` | All required PartLines have ReceivedStatus = Received (or marked NotNeeded) |
| `RepairScheduling` | `StartRepair` | `RepairInProgress` | Repair.ScheduledDate set, TechnicianId set |
| `RepairInProgress` | `CompleteRepair` | `RepairCompleted` | Repair.CompletionDate set, completion photos uploaded (≥1), intermediate inspection done if required |
| `RepairCompleted` | `IssueSettlement` | `Settlement` | Final quote and any deltas reconciled |
| `Settlement` | `ConfirmPayment` | `PaymentConfirmed` | Payment record(s) sum ≥ AgreedAmount or customer participation paid |
| `PaymentConfirmed` | `CloseCase` | `CaseClosed` | All Documents flagged "SentToInsurance" where required |
| any non-terminal | `Cancel` | `CaseClosed` (with reason) | Requires admin or branch-manager role |

### 5.2 System rule (from the docx)
> *"The workflow must validate all required documents, approvals, photos, and payments before allowing the case to move to the next stage."*

Every transition guard above is enforced server-side. UI disables the "advance" button until guards pass, and surfaces a checklist of remaining blockers.

---

## 6. Portal access matrix (per audience)

| Entity / Action | Staff (role-gated) | Customer | Insurance | Supplier |
|---|---|---|---|---|
| InsuranceCase: read | Yes — branch-scoped (Admin sees all) | Only own cases | Only cases at own InsuranceCompany | — |
| InsuranceCase: create | Receptionist+ | — | — | — |
| InsuranceCase: edit | Receptionist+ (limited) / BranchManager+ (full) | — | — | — |
| Assessment: write | Assessor or BodyShopManager | — | Read-only | — |
| Quote: generate PDF | BodyShopManager / Admin | View own | View assigned | — |
| InsuranceApproval: write | — | — | Insurance audience users | — |
| PartLine: read | Yes | View own | View assigned | Only lines where SupplierId = own |
| PartLine: order confirm | Receptionist+ | — | — | Supplier (mark Ordered/InTransit) |
| PartLine: mark Received | Receptionist+ at receiving branch | — | — | — |
| Repair: schedule | BranchManager / BodyShopManager | — | — | — |
| Repair: assign technician | BranchManager / BodyShopManager | — | — | — |
| Photo: upload | Staff | — | — | — |
| Photo: view | Staff | View own case | View assigned case | — |
| Document: upload | Staff | Limited types (Quote, Invoice received) | — | — |
| Payment: record | Receptionist+ | — | — | — |
| Customer: PII | Staff (with audit) | Self only | Limited (name, plate) | — |
| RetailCase: all | Staff | View own | — | — |

Branch scoping: a `BranchManager` sees only their `BranchId`. `Admin` and `BodyShopManager` see all branches. Implemented via a `BranchScopedQueryFilter` in EF Core.

---

## 7. Seed data plan

| Seed file | Source | Rows | Notes |
|---|---|---|---|
| `seed/body-panels.json` | `ΜΕΡΗ ΑΥΤΟΚΙΝΗΤΟΥ2.xlsx` cols A+B | ~78 | Includes data fix: 4 window-crank rows with duplicate Α/Α=67 will be reassigned 67, 67Α, 67Δ, 67Β to disambiguate. Fix the `ΕΠΙΣΚΕΥΥΗ` typo to `ΕΠΙΣΚΕΥΗ`. |
| `seed/body-panel-operations.json` | `ΜΕΡΗ ΑΥΤΟΚΙΝΗΤΟΥ2.xlsx` cols C–I matrix | ~300 | One row per (Panel, Operation) where the cell contained "Χ" |
| `seed/parts-catalog.json` | `ΜΕΡΗ ΑΥΤΟΚΙΝΗΤΟΥ.docx` | ~400 | Hierarchical taxonomy; populates a `PartCatalog` table (separate from per-case `PartLine`) |
| `seed/service-catalog.json` | image 1 diagram outer labels | 5 | Sun Films, Polish, Detail, External Repairs, Window Regulators |
| `seed/insurance-companies.json` | Manual list | ~20 | Major Greek insurers — Ergo, Interamerican, NN, Generali, etc. Editable after seed. |
| `seed/company-profile.json` | image 5 PDF header | 1 | Paint Bull, Παν. Τσαλδάρη 41, Ταύρος, 2102202898, VAT rate 24.00, logo path |
| `seed/branches.json` | TBD with user | 1+ | At least one default branch + warehouse |
| `seed/admin-user.json` | Generated | 1 | Default admin login printed at first run |

> **Status on `PartCatalog`:** still not modelled. `PartLine.PartName` remains free-text. Adding the taxonomy entity + seed is the next domain-model increment.

---

## 8. Open items

### Resolved during implementation
1. **Photos on RetailRepair** — ✅ `Photo` table extended with nullable `RetailRepairId` FK.
2. **Quote revisions** — ✅ kept as separate rows via the `IsCurrent` flag; the issue command flips prior currents to `false`.
3. **Greek myDATA integration** — ✅ Phase 11 landed `IMyDataClient` abstraction + stub. Real AADE adapter still pending.
4. **Damage diagram interactive UI** — ✅ `BodyPanelPicker.razor` renders the SVG with clickable hotspots driven by `BodyPanel.DiagramX/Y`.
5. **Default admin credentials** — ✅ auto-generated on first seed run, printed to console.
6. **Multi-tenancy readiness** — ✅ confirmed single-tenant. No `TenantId` on entities.
7. **Portal project split** — ✅ rejected. `Workshop.Portal.Customer/Insurance/Supplier` and `Workshop.Api` were deleted; all four audiences are served by `Workshop.Web` Blazor Server pages calling MediatR handlers directly.

### Still open
1. **`PartCatalog` entity** — the hierarchical parts taxonomy from `ΜΕΡΗ ΑΥΤΟΚΙΝΗΤΟΥ.docx` is not modelled. Decide whether the autocomplete catalog adds enough value to justify the entity + seed.
2. **`AuditLog` writer** — entity is in the schema but the EF interceptor only stamps `Created/UpdatedAt/By`. The PII-read audit promised in §6 is not enforced.
3. **Account management UI** — Identity supports password reset / email confirmation / lockout / MFA but no pages exist beyond Login + Logout. No staff/user admin page either.
4. **Webhook receivers** — once the real AADE adapter and a payment gateway land, their inbound webhooks (cancellations, async MARK confirmations, payment notifications) need endpoints inside `Workshop.Web` (e.g. `/webhooks/mydata/*`, `/webhooks/payments/*`). No separate API project — the four standalone scaffolds were deleted in favour of a single-app architecture.

---

## 9. Phase deliverables

All eleven phases have shipped. External-integration items in phases 5, 7, and 11 ship as abstractions + logging/stub adapters — see §1 for the swap-in points.

| Phase | Status | Deliverable |
|---|---|---|
| **0** | ✅ | Solution + 8 projects scaffolded; EF Core DbContext with all entities from §4; Identity with 4 portal audiences; bilingual resource files; MudBlazor theme; workflow state machine + unit tests; Docker compose; seed runner |
| **1** | ✅ | Reference data CRUD: Customers, Vehicles, Branches, Insurance Companies, Assessors, Adjusters, Suppliers; seed catalogs (BodyPanel, ServiceCatalog) — `PartCatalog` deferred (see §8) |
| **2** | ✅ | InsuranceCase CRUD + state machine wired to UI; `CaseEvent` history |
| **3** | ✅ | Assessment + clickable SVG damage diagram + WorkItems grid with allowed-ops validation |
| **4** | ✅ | Insurance Approval flow; Customer Assignment; first end-to-end insurance path |
| **5** | ✅ | Parts module: PartLine CRUD, branch routing, multi-state receipt, warehouse + storage location, supplier portal pages |
| **6** | ✅ | Repair scheduling, technician assignment, intermediate inspection, completion photos |
| **7** | ✅ | Documents + Photos (Assessment / Repair / RetailRepair) |
| **8** | ✅ | Settlement + Payment + Case Closure; Quote PDF generation (Invoice / Receipt / Case Form templates still TODO) |
| **9** | ✅ | Retail flow (RetailCase, RetailPartLine, RetailRepair, RetailCasePanel, RetailPayment) |
| **10** | ✅ | Dashboard KPIs on Home; `/reports` page with branch breakdown, aging, parts variance, technician productivity. Customer / Insurer / Supplier portal UIs are served inside `Workshop.Web` rather than the standalone projects |
| **11** | ✅ stubs | `IEmailSender` / `ISmsSender` / `IMyDataClient` abstractions + logging stub adapters; in-app notification bell; `SubmitQuoteToMyDataCommand` |

---

## 10. Out of scope (explicit)

- Mobile native apps (phones use the responsive web)
- Multi-tenant SaaS (single company, multiple branches only)
- Accounting integration beyond myDATA (no SAP/ERP sync)
- Historical data import from old Excel files (user confirmed: start fresh)
- Real-time chat between portals (notifications only)

---

*This document is the contract. If something here is wrong or missing, fix this file before changing code.*
