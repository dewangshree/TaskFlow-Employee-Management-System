import { api, cleanParams } from './client'
import type {
  AdminDashboard, AppNotification, CurrentUser, Employee, EmployeeDashboard, EmployeeLookup, Leave,
  LeaveStatus, LeaveType, ListParams, PagedResult, Profile, TaskDetail, TaskListItem, TaskPriority, TaskStatus,
} from './types'

const get = <T,>(url: string, params?: object) => api.get<T>(url, { params: params && cleanParams(params) }).then((r) => r.data)

export const authApi = {
  csrf: () => api.get('/auth/csrf'),
  me: () => get<CurrentUser>('/auth/me'),
  login: (body: { email: string; password: string; rememberMe: boolean }) => api.post<CurrentUser>('/auth/login', body).then((r) => r.data),
  register: (body: { firstName: string; lastName: string; email: string; department: string; password: string; confirmPassword: string }) =>
    api.post<CurrentUser>('/auth/register', body).then((r) => r.data),
  logout: () => api.post('/auth/logout'),
}

export const dashboardApi = {
  admin: () => get<AdminDashboard>('/dashboard/admin'),
  mine: () => get<EmployeeDashboard>('/dashboard/me'),
}

export interface TaskInput { title: string; description: string; priority: TaskPriority; dueDate: string; assigneeId: string | null }

export const tasksApi = {
  list: (p: ListParams) => get<PagedResult<TaskListItem>>('/tasks', p),
  mine: (p: ListParams) => get<PagedResult<TaskListItem>>('/tasks/assigned-to-me', p),
  get: (id: number) => get<TaskDetail>(`/tasks/${id}`),
  create: (body: TaskInput) => api.post<TaskDetail>('/tasks', body).then((r) => r.data),
  update: (id: number, body: TaskInput & { status: TaskStatus; version: number }) => api.put<TaskDetail>(`/tasks/${id}`, body).then((r) => r.data),
  updateStatus: (id: number, status: TaskStatus, version: number) =>
    api.patch<TaskDetail>(`/tasks/${id}/status`, { status, version }).then((r) => r.data),
  remove: (id: number) => api.delete(`/tasks/${id}`),
}

export const leavesApi = {
  mine: (p: ListParams) => get<PagedResult<Leave>>('/leaves/mine', p),
  list: (p: ListParams) => get<PagedResult<Leave>>('/leaves', p),
  apply: (body: { leaveType: LeaveType; startDate: string; endDate: string; reason: string }) => api.post<Leave>('/leaves', body).then((r) => r.data),
  review: (id: number, decision: Exclude<LeaveStatus, 'Pending'>, comment: string, version: number) =>
    api.put<Leave>(`/leaves/${id}/review`, { decision, comment, version }).then((r) => r.data),
}

export const employeesApi = {
  list: (p: ListParams) => get<PagedResult<Employee>>('/employees', p),
  setActive: (id: string, isActive: boolean) => api.put<Employee>(`/employees/${id}/active`, { isActive }).then((r) => r.data),
  assignable: () => get<EmployeeLookup[]>('/employees/assignable'),
  departments: () => get<string[]>('/employees/departments'),
}

export const profileApi = {
  get: () => get<Profile>('/profile'),
  update: (body: { firstName: string; lastName: string; email: string; department: string }) => api.put<Profile>('/profile', body).then((r) => r.data),
  changePassword: (body: { currentPassword: string; newPassword: string; confirmPassword: string }) => api.put('/profile/password', body),
}

export const notificationsApi = {
  list: (page: number, unreadOnly: boolean) => get<PagedResult<AppNotification>>('/notifications', { page, pageSize: 20, unreadOnly }),
  unreadCount: () => get<{ count: number }>('/notifications/unread-count'),
  markRead: (id: string) => api.post(`/notifications/${id}/read`),
  markAllRead: () => api.post('/notifications/read-all'),
}

/** Centralised query keys: one place to reason about cache invalidation. */
export const queryKeys = {
  me: ['me'] as const,
  dashboard: (role: string) => ['dashboard', role] as const,
  tasks: ['tasks'] as const,
  taskList: (scope: 'all' | 'mine', p: ListParams) => ['tasks', scope, p] as const,
  task: (id: number) => ['tasks', 'detail', id] as const,
  leaves: ['leaves'] as const,
  leaveList: (scope: 'all' | 'mine', p: ListParams) => ['leaves', scope, p] as const,
  employees: ['employees'] as const,
  employeeList: (p: ListParams) => ['employees', 'list', p] as const,
  assignable: ['employees', 'assignable'] as const,
  departments: ['employees', 'departments'] as const,
  profile: ['profile'] as const,
  notifications: ['notifications'] as const,
  unreadCount: ['notifications', 'unread'] as const,
}
