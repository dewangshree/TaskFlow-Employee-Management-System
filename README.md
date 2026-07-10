# TaskFlow — Employee Management System

TaskFlow is a full-stack, role-based Employee Management System built with **ASP.NET Core MVC**. It gives organizations a single dashboard to manage employees, assign and track tasks, and handle leave requests — with separate, permission-scoped experiences for **Admins** and **Employees**.

<p align="left">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/ASP.NET%20Core-MVC-512BD4?logo=dotnet&logoColor=white" alt="ASP.NET Core MVC">
  <img src="https://img.shields.io/badge/Entity%20Framework%20Core-10.0-blue" alt="EF Core">
  <img src="https://img.shields.io/badge/Database-SQLite-07405E?logo=sqlite&logoColor=white" alt="SQLite">
  <img src="https://img.shields.io/badge/Auth-ASP.NET%20Identity-005571" alt="ASP.NET Identity">
  <img src="https://img.shields.io/badge/UI-Bootstrap-7952B3?logo=bootstrap&logoColor=white" alt="Bootstrap">
</p>

---

## Overview

TaskFlow simplifies day-to-day HR and project-tracking workflows by centralizing three core functions in one application:

- **Employee management** — maintain profiles, departments, and account status
- **Task management** — create, assign, prioritize, and track tasks to completion
- **Leave management** — employees apply for leave, admins review and approve/reject with comments

The application uses **cookie-based authentication with role-based authorization** (Admin / Employee), so each user only sees the screens and actions relevant to their role.

---

## Screenshots

### Admin Dashboard
A real-time overview of employees, tasks, and leave requests, with quick access to recent activity.

![Admin Dashboard](screenshots/admin-dashboard.png)

### Task Management
Create, assign, filter, and update tasks by priority, status, and due date.

![Manage Tasks](screenshots/manage-tasks.png)

### Leave Request Management
Review, approve, or reject employee leave requests with search and status filters.

![Manage Leave Requests](screenshots/manage-leave-requests.png)

### Profile Management
Users can update their personal details and change their password securely.

![Edit Profile](screenshots/edit-profile.png)

---

## Key Features

**Authentication & Authorization**
- Secure login/registration powered by ASP.NET Core Identity
- Role-based access control (`Admin`, `Employee`)
- Enforced password policy (uppercase, lowercase, digit, special character)

**Admin Capabilities**
- Dashboard with live counts: total employees, total/pending/completed tasks, pending/approved leaves
- Full employee CRUD (create, view, update, deactivate)
- Create, assign, edit, and delete tasks with priority (Low/Medium/High/Critical) and status (Pending/In Progress/Completed/Cancelled) tracking
- Review, approve, or reject leave requests with an admin comment and audit trail
- Search and filter across tasks and leave requests

**Employee Capabilities**
- Personal dashboard of assigned tasks and leave history
- Apply for leave (Casual, Sick, Annual, Unpaid) and track approval status
- Update task progress
- Edit personal profile and change password

---

## Tech Stack

| Layer          | Technology                                              |
|----------------|----------------------------------------------------------|
| Framework      | ASP.NET Core MVC (.NET 10)                                |
| Language       | C#                                                        |
| ORM            | Entity Framework Core 10 (Code-First + Migrations)         |
| Database       | SQLite                                                     |
| Auth           | ASP.NET Core Identity (cookie-based, role-based)            |
| Frontend       | Razor Views, Bootstrap, jQuery, jQuery Validation           |
| Architecture   | MVC (Controllers / Models / ViewModels / Services / Views)  |

---

## Project Structure

```
TaskFlow/
├── Controllers/        # Account, Dashboard, Employees, Leaves, Tasks, Profile, Home
├── Data/                # AppDbContext (EF Core)
├── Helpers/             # RoleNames, PaginatedList
├── Migrations/          # EF Core database migrations
├── Models/              # ApplicationUser, TaskItem, LeaveRequest, Enums
├── Services/            # DashboardService, DbSeeder (demo data)
├── ViewModels/          # Strongly typed view models per feature
├── Views/               # Razor views organized by controller
└── wwwroot/             # Static assets (CSS, JS, Bootstrap, jQuery)
```

---

## Getting Started

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

On first run, the app seeds an admin account, three employee accounts, and sample tasks/leave requests:

| Role     | Email                     | Password      |
|----------|----------------------------|---------------|
| Admin    | admin@taskflow.com         | Admin@123     |
| Employee | john.doe@taskflow.com      | Employee@123  |
| Employee | jane.smith@taskflow.com    | Employee@123  |
| Employee | mike.johnson@taskflow.com  | Employee@123  |

---

## Author

**Shreyas Vikrant Dewangswami**
Final-year Information Science & Engineering student, Ramaiah Institute of Technology, Bengaluru

---

*This project was built as a demonstration of full-stack development skills using the ASP.NET Core MVC stack — including authentication, role-based authorization, EF Core data modeling, and CRUD-driven business workflows.*
