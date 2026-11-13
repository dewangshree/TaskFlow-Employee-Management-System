export type Role = 'Admin' | 'Employee'
export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Critical'
export type TaskStatus = 'Pending' | 'InProgress' | 'Completed' | 'Cancelled'
export type LeaveType = 'Casual' | 'Sick' | 'Annual' | 'Unpaid'
export type LeaveStatus = 'Pending' | 'Approved' | 'Rejected'
export type NotificationType = 'TaskAssigned' | 'TaskStatusChanged' | 'LeaveRequested' | 'LeaveReviewed'
export type SortDirection = 'Asc' | 'Desc'

export const TASK_PRIORITIES: TaskPriority[] = ['Low', 'Medium', 'High', 'Critical']
export const TASK_STATUSES: TaskStatus[] = ['Pending', 'InProgress', 'Completed', 'Cancelled']
export const LEAVE_TYPES: LeaveType[] = ['Casual', 'Sick', 'Annual', 'Unpaid']
export const LEAVE_STATUSES: LeaveStatus[] = ['Pending', 'Approved', 'Rejected']

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface ListParams {
  page: number
  pageSize: number
  search?: string
  sortBy?: string
  sortDirection: SortDirection
  [filter: string]: string | number | boolean | undefined
}

export interface CurrentUser {
  id: string
  email: string
  firstName: string
  lastName: string
  fullName: string
  department: string
  role: Role
}

export interface TaskListItem {
  id: number
  title: string
  priority: TaskPriority
  status: TaskStatus
  dueDate: string
  isOverdue: boolean
  assigneeId: string | null
  assigneeName: string | null
  createdByName: string
  createdAt: string
  version: number
}

export interface TaskDetail extends Omit<TaskListItem, 'createdByName'> {
  description: string
  createdById: string
  createdByName: string
  updatedAt: string | null
  allowedTransitions: TaskStatus[]
}

export interface Leave {
  id: number
  employeeId: string
  employeeName: string
  employeeDepartment: string
  leaveType: LeaveType
  startDate: string
  endDate: string
  totalDays: number
  reason: string
  status: LeaveStatus
  appliedOn: string
  adminComment: string | null
  reviewedByName: string | null
  reviewedAt: string | null
  version: number
}

export interface Employee {
  id: string
  firstName: string
  lastName: string
  fullName: string
  email: string
  department: string
  joinDate: string
  isActive: boolean
  totalTasks: number
  openTasks: number
}

export interface EmployeeLookup { id: string; fullName: string; department: string }

export interface TaskStats { total: number; pending: number; inProgress: number; completed: number; cancelled: number; overdue: number; open: number }
export interface LeaveStats { pending: number; approved: number; rejected: number }
export interface RecentTask { id: number; title: string; priority: TaskPriority; status: TaskStatus; dueDate: string; isOverdue: boolean; assigneeName: string | null }
export interface RecentLeave { id: number; employeeName: string; leaveType: LeaveType; startDate: string; endDate: string; status: LeaveStatus; appliedOn: string }

export interface AdminDashboard {
  totalEmployees: number
  activeEmployees: number
  employeesOnLeaveToday: number
  tasks: TaskStats
  leaves: LeaveStats
  recentTasks: RecentTask[]
  recentLeaves: RecentLeave[]
  generatedAt: string
}

export interface EmployeeDashboard {
  tasks: TaskStats
  leaves: LeaveStats
  approvedLeaveDaysThisYear: number
  upcomingTasks: RecentTask[]
  recentLeaves: RecentLeave[]
  generatedAt: string
}

export interface Profile {
  id: string
  firstName: string
  lastName: string
  email: string
  department: string
  joinDate: string
  role: Role
  assignedTasks: number
  completedTasks: number
  pendingLeaveRequests: number
}

export interface AppNotification {
  id: string
  type: NotificationType
  title: string
  message: string
  link: string | null
  isRead: boolean
  createdAt: string
}

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  code?: string
  errors?: Record<string, string[]>
  correlationId?: string
  retryAfterSeconds?: number
}
