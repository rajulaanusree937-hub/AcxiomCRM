\# AcxiomCRM



A role-based \*\*Customer Relationship Management\*\* web application built with \*\*ASP.NET Core MVC (.NET 10)\*\*,

covering the full customer-sales lifecycle — lead capture, follow-ups, opportunities and pipeline —

with secure authentication, role-based authorization, audit logging, a secured REST API and reporting.



\---



\## Tech Stack



| Layer | Technology |

|---|---|

| Framework | ASP.NET Core MVC on \*\*.NET 10 (LTS)\*\*, C# |

| Authentication | \*\*ASP.NET Core Identity\*\* (hashing, password policy, lockout, roles) + \*\*JWT\*\* for the API |

| Data | \*\*Entity Framework Core\*\* + \*\*SQL Server\*\* (LocalDB for development) |

| UI | Razor Views, \*\*Bootstrap 5\*\*, Bootstrap Icons, jQuery Unobtrusive Validation |

| Charts | \*\*Chart.js\*\* |

| API docs | OpenAPI + Scalar |

| Testing | Postman (collection in `docs/`) |



\---



\## Modules



| Module | Highlights |

|---|---|

| \*\*Authentication\*\* | Login, Register, Logout, password policy, lockout after 5 failed attempts, deactivated-user blocking |

| \*\*Dashboard\*\* | 8 role-scoped KPI cards, date filter (Today / Week / Month / Custom), 3 Chart.js charts, upcoming follow-ups, admin security stats |

| \*\*Customers\*\* | CRUD, search/filter/sort/paging, unique email \& phone, duplicate prevention, auto code `CUS-00001`, history |

| \*\*Leads\*\* | CRUD, status workflow (invalid transitions blocked), \*\*lead → customer (+ opportunity) conversion\*\* in a transaction |

| \*\*Opportunities\*\* | CRUD, pipeline stages, weighted value (Amount × Probability ÷ 100), Mark Won/Lost, \*\*Kanban pipeline view\*\* |

| \*\*Follow-Ups\*\* | Linked to customer/lead/opportunity, Upcoming/Today/Overdue views, mark completed, reschedule |



| \*\*Activities\*\* | Call / Meeting / Email / Task logging |

| \*\*Users \& Roles\*\* | Admin-only: create users, assign one role, activate/deactivate, unlock, reset password, permission matrix |

| \*\*Audit Log\*\* | Automatic Create/Update/Delete tracking with old/new values + security events (login, failed login, lockout, role change…). \*\*Append-only\*\* (DB trigger) |

| \*\*REST API\*\* | JWT-secured endpoints for auth, customers, leads, opportunities, follow-ups and pipeline report |

| \*\*Reports\*\* | Customer, Lead, Follow-Up, Opportunity, Pipeline (stage/owner), Conversion, User Activity, Audit — filters, sorting, paging, \*\*CSV export\*\* |



\---



\## Roles \& Permissions



| Module | Admin | Manager | Sales Executive |

|---|---|---|---|

| CRM records (customers, leads, opportunities, follow-ups, activities) | Full | Team (all sales records) | Own / assigned only |

| Dashboard \& reports | All | Team | Own / assigned |

| User \& role management | Full | No | No |

| Audit log | Full | No | No |



Authorization is enforced \*\*on the server\*\* — global login filter, role policies, and per-record ownership checks

(changing a record ID in the URL returns \*Access Denied\*).



\---



\## Architecture



```

AcxiomCRM/

├── Areas/Identity/        Login, Register, Logout (customized Identity pages)

├── Controllers/           MVC controllers (thin)

│   └── Api/               REST API controllers (JWT)

├── Data/                  ApplicationDbContext, migrations, seeder, automatic audit trail

├── DTOs/                  API request/response models (no entities exposed)

├── Models/                Entities, enums, roles/policies, validation attributes



├── Services/              Business rules, ownership filtering, audit, reports, tokens

├── ViewModels/            Form and list models with validation

└── Views/                 Razor views (Bootstrap 5)

```



Request flow: \*\*Browser/API → Controller → Service (business rules) → EF Core → SQL Server\*\*,

with security, validation and auditing applied across all layers.



\---



\## Validation



\- \*\*Client-side:\*\* Required, Email, Phone (10-digit Indian mobile), Length, Date (not in the past), Numeric ranges.

\- \*\*Server-side:\*\* every rule repeated in `ModelState` and in the services, so crafted requests are still rejected.

\- \*\*Business rules:\*\* Amount > 0 for active opportunities · Probability 0–100 · Expected Close Date not in the past ·

&#x20; Follow-up date not before today · unique email/phone · valid status values only · lead status workflow.



\## Security



\- Passwords stored only as Identity hashes; password policy (8+ chars, upper, lower, digit, symbol)

\- Account lockout (5 attempts / 15 minutes), unlock by admin

\- Role-based + ownership authorization on every page and API endpoint

\- Anti-forgery tokens on all state-changing forms (global filter)

\- EF Core parameterized queries (no SQL string concatenation)

\- Secure cookies (HttpOnly, HTTPS-only), HSTS, security headers

\- JWT for the API, login rate limiting (5/min/IP → 429)

\- Audit log for security/business events — never stores passwords, hashes or tokens

\- Secrets kept out of source control (User Secrets / environment variables)



\---



\## Getting Started





\### Prerequisites

\- Visual Studio 2026 with the \*\*ASP.NET and web development\*\* workload (includes .NET 10 SDK and SQL Server LocalDB)



\### Setup

```bash

git clone https://github.com/rajulaanusree937-hub/AcxiomCRM.git

cd AcxiomCRM/AcxiomCRM



\# 1. JWT signing key (kept outside the repository)

dotnet user-secrets set "Jwt:Key" "<any random string of at least 32 characters>"



\# 2. Create the database

dotnet tool install --global dotnet-ef

dotnet ef database update



\# 3. Run

dotnet run

```

Or open `AcxiomCRM.slnx` in Visual Studio and press \*\*F5\*\*.



\### Demo accounts (created automatically in Development only)



| Role | Email | Password |

|---|---|---|

| Admin | admin@acxiom.com | Admin@123 |

| Manager | manager@acxiom.com | Manager@123 |

| Sales Executive | sales@acxiom.com | Sales@123 |



\### Production configuration

Set these as environment variables (never in files):

\- `ConnectionStrings\_\_DefaultConnection`



\- `Jwt\_\_Key`



\---



\## REST API



| Method | Endpoint | Access |

|---|---|---|

| POST | `/api/auth/login` | Public, rate limited |

| POST | `/api/auth/logout` | Authenticated |

| GET | `/api/auth/me` | Authenticated |

| GET / POST | `/api/customers` | Authorized |

| GET / PUT / DELETE | `/api/customers/{id}` | Authorized |

| GET / POST | `/api/leads` · GET `/api/leads/{id}` | Authorized |

| GET / POST | `/api/opportunities` · GET `/api/opportunities/{id}` | Authorized |

| GET / POST | `/api/followups` | Authorized |

| GET | `/api/reports/pipeline` | Manager / Admin |



Status codes: \*\*200, 201 (with Location), 400, 401, 403, 404, 409, 429\*\*.

Interactive docs (Development): `https://localhost:7109/scalar/v1`

Postman collection: `docs/AcxiomCRM.postman\_collection.json`



\---



\## Testing



All 14 scenarios of the final acceptance checklist plus the security checklist were executed —

see \*\*\[TEST-RESULTS.md](TEST-RESULTS.md)\*\*.



\## Build History



Built in the order of the project's implementation sequence — one commit per step:



1\. Solution structure \& database · 2. Identity, roles, lockout · 3. Authorization \& ownership ·

4\. Layout \& dashboard shell · 5. Customers · 6. Leads \& conversion · 7. Follow-ups \& activities ·

8\. Opportunities \& pipeline · 9. Validation · 10. Audit log + user/role management ·

11\. REST API · 12. Dashboard KPIs, charts \& reports · 13. Testing · 14. Production security \& documentation

