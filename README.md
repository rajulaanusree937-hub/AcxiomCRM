# AcxiomCRM — Role-Based CRM Application

AcxiomCRM is a web-based CRM used by a sales organization to manage **customers, leads, opportunities, follow-ups, activities, the sales pipeline, users & roles and audit logs**. Sales users manage their assigned records while managers monitor the complete pipeline through a dashboard.

Built with **ASP.NET Core MVC (.NET 10)**, **ASP.NET Core Identity**, **Entity Framework Core**, **SQL Server**, **Bootstrap 5** and **Chart.js**.

---

### 🔗 Live Demo
**https://acxiomcrm-anusree.runasp.net**

## 1. Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core MVC · .NET 10 (LTS) · C# |
| Authentication | ASP.NET Core Identity (web) · JWT Bearer (REST API) |
| Data access | Entity Framework Core 10 |
| Database | SQL Server (LocalDB in development) |
| UI | Razor Views · Bootstrap 5 · Bootstrap Icons |
| Validation | Data Annotations · jQuery Unobtrusive Validation · custom server rules |
| Charts | Chart.js |
| API docs / testing | OpenAPI + Scalar · Postman |

---

## 2. Module Structure

| # | Module | What it does |
|---|---|---|
| 1 | **AcxiomCRM shell** | Responsive Bootstrap layout, role-based menu, alerts, confirmation dialogs, pagination |
| 2 | **Authentication & Authorization** | Login, Register, Logout, password policy, lockout, roles, ownership rules |
| 3 | **Dashboard** | 8 role-scoped KPI cards, date filter, 3 Chart.js charts, upcoming follow-ups, admin security stats |
| 4 | **Customer Management** | Create / Edit / Details / Delete / Search, unique email & phone, history |
| 5 | **Lead Management** | CRUD, status workflow, assignment, **lead → customer (+ opportunity) conversion** |

| 6 | **Follow-Up Management** | Schedule, complete, reschedule, upcoming / overdue views |
| 7 | **Activity Management** | Call, Meeting, Email, Task |
| 8 | **Opportunity** | Pipeline stages, amount, probability, weighted value, Won / Lost, Kanban pipeline view |
| 9 | **User & Role Management** | Admin: create users, one role each, activate / deactivate, unlock, reset password |
| 10 | **Audit Log** | Automatic create / update / delete tracking with old & new values + security events, append-only |
| 11 | **REST API** | JWT-secured endpoints for auth, customers, leads, opportunities, follow-ups, pipeline report |
| 12 | **Reports** | 8 reports with filters, sorting, paging and CSV export |

---

## 3. Application Architecture

Layered architecture — each layer has one responsibility:

```
Users
  ↓
Presentation Layer      Views/ (Razor + Bootstrap), client-side validation
  ↓
Application Layer       Controllers/ (MVC) · Controllers/Api/ (REST) · ViewModels/ · DTOs/
  ↓
Business Layer          Services/ — business rules, workflows, ownership, audit, reports
  ↓
Data Access Layer       Data/ApplicationDbContext (EF Core) · Migrations
  ↓
Database Layer          SQL Server — constraints, unique indexes, audit trigger

Security + Logging + Validation + Auditing → across all layers
```

```
AcxiomCRM/

├── Areas/Identity/      Login, Register, Logout (customized)
├── Controllers/         MVC controllers
│   └── Api/             REST API controllers
├── Data/                DbContext, migrations, seeder, automatic audit trail
├── DTOs/                API request / response models
├── Models/              Entities, enums, roles, policies, validation attributes
├── Services/            Business logic
├── ViewModels/          Form & list models with validation
└── Views/               Razor views
```

---

## 4. Implementation Sequence

The project was built following the document's recommended sequence — **one commit per step** (see *Commits*).

| Step | What was implemented | Key files |
|---|---|---|
| **1. Solution structure & database** | MVC project, folder structure, 6 entities (Customer, Lead, Opportunity, FollowUp, Activity, AuditLog), unique indexes | `Models/`, `Data/ApplicationDbContext.cs` |
| **2. Users, roles & authentication** | Identity with `ApplicationUser` (FullName, IsActive), password policy, lockout, 3 roles, customized Login/Register | `Program.cs`, `Data/DbSeeder.cs`, `Areas/Identity/` |
| **3. Authorization** | Global login requirement, role policies, record ownership (SalesExecutive sees only assigned records) | `Services/OwnershipExtensions.cs`, `Models/AppPolicies.cs` |
| **4. Layout & dashboard shell** | Role-based navbar, alerts, delete confirmation, dashboard after login | `Views/Shared/_Layout.cshtml` |
| **5. Customer Management** | CRUD, search / filter / sort / paging, duplicate prevention, auto code `CUS-00001` | `CustomersController`, `CustomerService` |
| **6. Lead Management** | CRUD, status workflow, conversion in a transaction | `LeadsController`, `LeadService` |
| **7. Follow-Up Management** | Related to customer / lead / opportunity, date rule, overdue view, plus Activities | `FollowUpsController`, `ActivitiesController` |
| **8. Opportunity Management** | Stages, weighted pipeline, Won / Lost, Kanban view | `OpportunitiesController`, `OpportunityService` |
| **9. Validation** | Client + server validation, custom `[NotInPast]` rule, enum checks, global anti-forgery | `Models/Validation/`, `wwwroot/js/site.js` |
| **10. Audit Log** | Automatic change tracking, login / security events, admin page, append-only trigger, User & Role management | `ApplicationDbContext`, `AuditService`, `UsersController` |
| **11. REST API** | JWT auth, DTOs, status codes, rate limiting, OpenAPI docs | `Controllers/Api/`, `DTOs/`, `TokenService` |
| **12. Reports & dashboard KPIs** | Real KPI cards, Chart.js charts, 8 reports with CSV export | `DashboardService`, `ReportService` |
| **13. Testing** | Full acceptance & security checklist | `TEST-RESULTS.md` |

| **14. Production settings** | User Secrets, security headers, production error handling, demo users dev-only | `Program.cs`, `appsettings.Production.json` |

---

## 5. Roles & Permissions

| Module | Admin | Manager | Sales Executive |
|---|---|---|---|
| Dashboard | Full | Team | Own / Assigned |
| Customers · Leads · Opportunities · Follow-Ups · Activities | Full | Team | Own / Assigned |
| User Management | Full | No | No |
| Role Management | Full | No | No |
| Audit Log | Full | No | No |
| Reports | All | Management / team | Own / assigned |

Enforced **on the server**, not just by hiding links — opening another user's record by changing the URL returns *Access Denied*.

---

## 6. Core Workflows

- **Lead → Customer:** Create → Assign → Contact → Qualify → **Convert** (creates customer + optional opportunity) → audited
- **Opportunity:** Create → Amount / Probability / Close date → move through stages → **Mark Won / Lost** → final outcome
- **Follow-Up:** Schedule → Assign → Complete / Missed / Reschedule → shown in customer history → audited
- **User Administration:** Create → Assign role → Activate / Deactivate → Unlock / Reset password → audited

---

## 7. Validation

| Type | Client-side | Server-side |
|---|---|---|

| Required fields | ✅ | ✅ |
| Email format | ✅ | ✅ |
| Phone (10-digit Indian mobile) | ✅ | ✅ |
| Length limits | ✅ | ✅ |
| Dates not in the past | ✅ custom `[NotInPast]` | ✅ |
| Numeric ranges | ✅ | ✅ |

**Business rules (server):** Opportunity Amount > 0 · Probability 0–100 · Expected Close Date not in the past (active) · Follow-up date not before today · unique customer email & phone · duplicate customer blocked · only valid status values accepted · lead status workflow · related records must exist and be accessible.

---

## 8. Security

- Passwords stored only as Identity hashes · password policy (8+ chars, upper, lower, digit, symbol)
- Account lockout after 5 failed attempts (15 min) · admin unlock
- Role-based + record-ownership authorization on every page and API endpoint
- Anti-forgery tokens on all state-changing forms (global filter)
- EF Core parameterized queries — no SQL string concatenation
- HTTPS-only, HttpOnly cookies · HSTS · security headers
- JWT for the API · login rate limiting (5 / min / IP)
- Audit log never stores passwords, hashes or tokens · append-only via database trigger
- Secrets kept out of source control (User Secrets / environment variables)

---

## 9. REST API

| Method | Endpoint | Access |
|---|---|---|
| POST | `/api/auth/login` | Public · rate limited |
| POST | `/api/auth/logout` | Authenticated |
| GET | `/api/auth/me` | Authenticated |

| GET · POST | `/api/customers` | Authorized |
| GET · PUT · DELETE | `/api/customers/{id}` | Authorized |
| GET · POST | `/api/leads` | Authorized |
| GET · POST | `/api/opportunities` | Authorized |
| GET · POST | `/api/followups` | Authorized |
| GET | `/api/reports/pipeline` | Manager / Admin |

Returns **200, 201 (with Location), 400, 401, 403, 404, 409, 429** · DTOs only · no passwords or internal fields.

---

## 10. Reports

Customer · Lead · Follow-Up · Opportunity · Pipeline (stage-wise / owner-wise) · Sales & Conversion · User Activity (Manager / Admin) · Audit (Admin) — all with **date filters, owner filter, sorting, paging and CSV export**.

---

## 11. Testing

All **14 scenarios of the Final Acceptance Checklist** and the **Security Checklist** were executed and passed — see [TEST-RESULTS.md](TEST-RESULTS.md).

---

## 12. Getting Started

**Prerequisites:** Visual Studio 2026 with the *ASP.NET and web development* workload (includes .NET 10 SDK and SQL Server LocalDB).

```bash
git clone https://github.com/rajulaanusree937-hub/AcxiomCRM.git
cd AcxiomCRM/AcxiomCRM

dotnet user-secrets set "Jwt:Key" "<any random text, at least 32 characters>"

dotnet tool install --global dotnet-ef
dotnet ef database update
dotnet run
```

Or open `AcxiomCRM.slnx` in Visual Studio and press **F5**.

**Demo accounts** (created automatically in Development):

| Role | Email | Password |
|---|---|---|
| Admin | admin@acxiom.com | Admin@123 |
| Manager | manager@acxiom.com | Manager@123 |
| Sales Executive | sales@acxiom.com | Sales@123 |

**Production:** set `ConnectionStrings__DefaultConnection` and `Jwt__Key` as environment variables.
