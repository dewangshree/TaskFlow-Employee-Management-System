<div align="center">

# 🗂️ TaskFlow — Employee Management System

### Role-Based · Secure · Full-Stack ASP.NET Core MVC Application

TaskFlow is a role-based Employee Management System that lets **Admins** manage employees, assign and track tasks, and approve leave — while **Employees** get a scoped, permission-limited workspace to manage their own tasks and leave requests. Authorization is enforced at the controller level with ASP.NET Core Identity, and all business data is modeled relationally through EF Core.

🚀 **[Live Demo →](https://taskflow-shreyas-svd2026-c9bbcsezbmbjbvbu.centralindia-01.azurewebsites.net/)**

</div>

---

## 📋 Table of Contents

- [System Overview](#-system-overview)
- [End-to-End System Flow](#-end-to-end-system-flow)
- [Database Relationship Model](#-database-relationship-model)
- [Security Model — Role-Based Authorization](#-security-model--role-based-authorization)
- [Authentication Flow](#-authentication-flow)
- [Dashboard Aggregation Flow](#-dashboard-aggregation-flow)
- [Key Engineering Decisions](#-key-engineering-decisions)
- [Challenges & Solutions](#-challenges--solutions)
- [Time Investment](#-time-investment)
- [Live Deployment](#-live-deployment)
- [Local Setup](#-local-setup)
- [Project Structure](#-project-structure)
- [What This Project Demonstrates](#-what-this-project-demonstrates)

---

## 🔷 System Overview

> TaskFlow gives Admins full oversight of the workforce — employees, tasks, and leave — while Employees get a scoped self-service view limited to their own work and requests.

| Feature | Implementation |
|---|---|
| Authentication | ASP.NET Core Identity (cookie-based sessions) |
| Authorization | Role-based, enforced per-controller/action (`Admin`, `Employee`) |
| Data Access | Entity Framework Core (Code-First, migrations) |
| Database | SQLite |
| Frontend | Razor Views + Bootstrap + jQuery |
| Architecture | MVC (Controllers → Services → EF Core → SQLite) |

---

## 🔁 End-to-End System Flow

```
┌──────────────────────────────────────────────────────────────┐
│                           USER                                │
│               Admin or Employee (Web Browser)                 │
└──────────────────────────┬────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                         │
│  • Razor Views (per role/feature)                              │
│  • Bootstrap UI + jQuery Validation                            │
└──────────────────────────┬────────────────────────────────────┘
                            │  HTTP Request + Auth Cookie
                            ▼
┌──────────────────────────────────────────────────────────────┐
│                     MVC CONTROLLER LAYER                       │
│  • [Authorize] / [Authorize(Roles = ...)] guards                │
│  • Employees, Tasks, Leaves, Dashboard, Profile, Account         │
└──────────────────────────┬────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────────┐
│                      SERVICE LAYER                              │
│  • DashboardService (aggregates counts & recent activity)        │
└──────────────────────────┬────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────────┐
│                  DATA ACCESS LAYER (EF Core)                    │
│  • AppDbContext (IdentityDbContext<ApplicationUser>)             │
│  • TaskItems, LeaveRequests, Users, Roles                        │
└──────────────────────────┬────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────────┐
│                      SQLITE DATABASE                             │
│  • Relational storage, migration-managed schema                  │
└──────────────────────────────────────────────────────────────┘
```

---

## 🗄 Database Relationship Model

```
AspNetUsers (ApplicationUser)  (1)
    │
    ├── AssignedTasks  (*)  ──►  TaskItems.AssignedToId   [SetNull on delete]
    ├── CreatedTasks   (*)  ──►  TaskItems.CreatedById    [Restrict on delete]
    └── LeaveRequests  (*)  ──►  LeaveRequests.EmployeeId [Cascade on delete]
```

**Core entities:**

```csharp
ApplicationUser : IdentityUser
    ├── FirstName, LastName, Department, JoinDate, IsActive
    ├── AssignedTasks : ICollection<TaskItem>
    ├── CreatedTasks  : ICollection<TaskItem>
    └── LeaveRequests : ICollection<LeaveRequest>

TaskItem
    ├── Title, Description, Priority, Status, DueDate, CreatedAt
    ├── AssignedToId → ApplicationUser (nullable, SetNull on delete)
    └── CreatedById   → ApplicationUser (required, Restrict on delete)

LeaveRequest
    ├── LeaveType, StartDate, EndDate, Reason, Status, AppliedOn
    ├── AdminComment (set on approve/reject)
    └── EmployeeId → ApplicationUser (Cascade on delete)
```

> A task can be unassigned (employee deactivated/removed) but never loses its creator — enforced via `DeleteBehavior.SetNull` vs. `DeleteBehavior.Restrict`. Leave requests are tied to their employee's lifecycle via `DeleteBehavior.Cascade`.

---

## 🔐 Security Model — Role-Based Authorization

Every controller and action is explicitly guarded — there is no reliance on hiding UI elements alone.

```
[Authorize]                              → any authenticated user
[Authorize(Roles = RoleNames.Admin)]     → Admin-only actions
[Authorize(Roles = RoleNames.Employee)]  → Employee-only actions
```

| Controller  | Admin-only actions | Employee-only actions |
|---|---|---|
| `EmployeesController` | Full CRUD on employee records | — |
| `TasksController`     | Create, assign, edit, delete any task | Apply for a task, update own task status |
| `LeavesController`    | Approve / reject with comment, view all requests | Apply for leave, view own leave history |
| `DashboardController` | Org-wide aggregated dashboard | Personal dashboard scoped to own data |

```
✅ Employees can never view or modify another employee's tasks or leave
✅ Admins get organization-wide visibility; Employees get self-service only
✅ Enforcement happens server-side on every controller action, not just in the UI
✅ Password policy enforced via ASP.NET Identity (upper/lower/digit/special char, min length 6)
```

---

## 🔑 Authentication Flow

```
User submits Login form (email + password)
            │
            ▼
    AccountController (Login action)
            │
            ▼
    ASP.NET Core Identity (SignInManager)
            │
            ▼
    Credentials validated against AspNetUsers (hashed)
            │
            ▼
    Auth cookie issued (8-hour sliding expiration)
            │
            ▼
    Role claims (Admin / Employee) attached to identity
            │
            ▼
    Subsequent requests authorized via [Authorize] attributes
```

> No third-party auth provider — credentials are hashed and managed entirely through ASP.NET Core Identity's built-in `UserManager` / `SignInManager`.

---

## 📊 Dashboard Aggregation Flow

```
Request hits DashboardController
            │
            ▼
    Is user in Admin role?
       │              │
      Yes             No
       │              │
       ▼              ▼
GetAdminDashboardAsync()   GetEmployeeDashboardAsync(userId)
       │                          │
       ▼                          ▼
Counts across ALL:          Counts scoped to
• Employees                 the current user's:
• Tasks (total/pending/     • Tasks
  completed)                • Leave requests
• Leave requests
       │                          │
       └──────────┬───────────────┘
                   ▼
      Top 5 recent tasks + top 5 recent
      leave requests (ordered by date)
                   ▼
      Rendered to role-specific dashboard view
```

> A single `DashboardService` serves both roles — the query scope (org-wide vs. user-scoped) is the only thing that changes, keeping the aggregation logic in one place.

---

## 🧠 Key Engineering Decisions

| Decision | Rationale |
|---|---|
| Server-side `[Authorize]` on every action | Authorization can't be bypassed by hiding UI — enforced at the controller, not the view |
| Single `DashboardService` for both roles | Avoids duplicated aggregation logic; scope is parameterized, not re-implemented |
| `DeleteBehavior.SetNull` vs. `Restrict` vs. `Cascade` | Chosen per relationship to reflect real business rules (a task survives an employee's removal; a leave request doesn't) |
| SQLite for the data layer | Zero-config, file-based database — ideal for a self-contained demo/portfolio deployment |
| Code-First EF Core migrations | Schema is version-controlled and reproducible via `dotnet ef database update` |
| `PaginatedList<T>` helper | Reusable, generic pagination across Tasks and Leave Requests tables instead of duplicating `Skip/Take` logic |
| Seeded demo data (`DbSeeder`) | Reviewers/recruiters can run the app immediately with realistic sample data — no manual setup required |

---

## 🛠 Challenges & Solutions

**1. Preventing role-based data leakage**

```
Issue:    Employees should never see other employees' tasks or leave requests
Solution: Scoped every Employee-facing query by the authenticated user's ID
          at the controller/service level (not filtered client-side)
```

**2. Modeling task ownership vs. task assignment**

```
Issue:    A task needs both a "creator" (always an Admin) and an "assignee"
          (can become null if the employee is removed)
Solution: Two separate foreign keys (CreatedById, AssignedToId) with
          different EF Core delete behaviors (Restrict vs. SetNull)
```

**3. Sharing dashboard logic across two very different views**

```
Issue:    Admin and Employee dashboards show similar cards but very
          different data scopes
Solution: Single DashboardService with GetAdminDashboardAsync() and
          GetEmployeeDashboardAsync(userId) sharing the same private
          helper methods for "recent tasks" / "recent leave requests"
```

---

## ⏱ Time Investment

```
Architecture & Data Modeling     ──────────  3 hours
Identity & Role-Based Auth       ──────────  3 hours
Task Management Module           ──────────  4 hours
Leave Management Module          ──────────  3 hours
Dashboard & Aggregation Logic    ──────────  2 hours
UI Polish (Bootstrap/Razor)      ──────────  2 hours
Seeding + Testing + Docs         ──────────  2 hours
                                 ─────────────────────
Total                                      ~19 hours
```

---

## 🌐 Live Deployment

The application is deployed on **Azure App Service**.

🔗 **[https://taskflow-shreyas-svd2026-c9bbcsezbmbjbvbu.centralindia-01.azurewebsites.net/](https://taskflow-shreyas-svd2026-c9bbcsezbmbjbvbu.centralindia-01.azurewebsites.net/)**

Use the demo credentials below to log in as either an Admin or an Employee.

---

## 🚀 Local Setup

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Setup

```bash
# Clone the repository
git clone https://github.com/dewangshree/TaskFlow-Employee-Management-System.git
cd TaskFlow-Employee-Management-System

# Restore dependencies
dotnet restore

# Apply database migrations (creates taskflow.db)
dotnet ef database update

# Run the application
dotnet run
```

The app will be available at `https://localhost:5198` (or the port shown in your terminal).

### Demo Credentials

Seeded automatically on first run:

| Role     | Email                       | Password      |
|----------|------------------------------|----------------|
| Admin    | admin@taskflow.com           | Admin@123      |
| Employee | john.doe@taskflow.com        | Employee@123   |
| Employee | jane.smith@taskflow.com      | Employee@123   |
| Employee | mike.johnson@taskflow.com    | Employee@123   |

---

## 📁 Project Structure

```
TaskFlow/
├── Controllers/        # Account, Dashboard, Employees, Leaves, Tasks, Profile, Home
├── Data/                # AppDbContext (EF Core, IdentityDbContext)
├── Helpers/             # RoleNames, PaginatedList<T>
├── Migrations/          # EF Core database migrations
├── Models/              # ApplicationUser, TaskItem, LeaveRequest, Enums
├── Services/            # DashboardService, DbSeeder (demo data)
├── ViewModels/          # Strongly typed view models per feature
├── Views/               # Razor views organized by controller
└── wwwroot/             # Static assets (CSS, JS, Bootstrap, jQuery)
```

---

## ✅ What This Project Demonstrates

```
✔  Role-based authorization enforced server-side, not just in the UI
✔  Relational data modeling with EF Core (foreign keys, delete behaviors)
✔  Clean separation of Controllers → Services → Data Access
✔  Reusable, generic components (PaginatedList<T>, shared DashboardService)
✔  ASP.NET Core Identity: hashed credentials, cookie auth, password policy
✔  Reproducible setup via Code-First migrations + seeded demo data
✔  Structured, recruiter-readable engineering documentation
```

---

## 👤 Author

**Shreyas Vikrant Dewangswami**

---

<div align="center">

**Built with ASP.NET Core MVC · Entity Framework Core · SQLite · Bootstrap**

⭐ Star this repo if you found it helpful!

</div>
