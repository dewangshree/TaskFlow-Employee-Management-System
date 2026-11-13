import { lazy } from 'react'
import { Navigate, Route, Routes } from 'react-router'
import { AnonymousOnly, RequireAuth, RequireRole } from './auth/Guards'
import { AppShell } from './components/AppShell'

// Route-level code splitting: each page is its own chunk, loaded on demand.
const LoginPage = lazy(() => import('./features/auth/LoginPage'))
const RegisterPage = lazy(() => import('./features/auth/RegisterPage'))
const DashboardPage = lazy(() => import('./features/dashboard/DashboardPage'))
const TaskListPage = lazy(() => import('./features/tasks/TaskListPage'))
const TaskDetailPage = lazy(() => import('./features/tasks/TaskDetailPage'))
const MyLeavesPage = lazy(() => import('./features/leaves/LeavePages').then((m) => ({ default: m.MyLeavesPage })))
const ManageLeavesPage = lazy(() => import('./features/leaves/LeavePages').then((m) => ({ default: m.ManageLeavesPage })))
const EmployeesPage = lazy(() => import('./features/employees/EmployeesPage'))
const ProfilePage = lazy(() => import('./features/profile/ProfilePage'))
const NotificationsPage = lazy(() => import('./features/notifications/NotificationsPage'))

export default function App() {
  return (
    <Routes>
      <Route element={<AnonymousOnly />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
      </Route>
      <Route element={<RequireAuth />}>
        <Route element={<AppShell />}>
          <Route index element={<DashboardPage />} />
          <Route path="tasks/:id" element={<TaskDetailPage />} />
          <Route path="notifications" element={<NotificationsPage />} />
          <Route path="profile" element={<ProfilePage />} />
          <Route element={<RequireRole role="Admin" />}>
            <Route path="tasks" element={<TaskListPage scope="all" />} />
            <Route path="leaves/manage" element={<ManageLeavesPage />} />
            <Route path="employees" element={<EmployeesPage />} />
          </Route>
          <Route element={<RequireRole role="Employee" />}>
            <Route path="my-tasks" element={<TaskListPage scope="mine" />} />
            <Route path="leaves" element={<MyLeavesPage />} />
          </Route>
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
