import type { LeaveStatus, TaskPriority, TaskStatus } from '../api/types'

const dateFmt = new Intl.DateTimeFormat(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
const dateTimeFmt = new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })

/** API dates (yyyy-MM-dd) are calendar dates; parse as local to avoid off-by-one timezone shifts. */
export function formatDate(value: string | null | undefined) {
  if (!value) return '—'
  const [y, m, d] = value.slice(0, 10).split('-').map(Number)
  return dateFmt.format(new Date(y, m - 1, d))
}

export const formatDateTime = (value: string) => dateTimeFmt.format(new Date(value))

export function relativeTime(value: string) {
  const seconds = Math.round((Date.now() - new Date(value).getTime()) / 1000)
  if (seconds < 60) return 'just now'
  const minutes = Math.round(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.round(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.round(hours / 24)
  return days < 7 ? `${days}d ago` : formatDate(value)
}

export const todayIso = () => {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export const statusLabel = (s: TaskStatus) => (s === 'InProgress' ? 'In Progress' : s)

type ChipColor = 'default' | 'primary' | 'success' | 'warning' | 'error' | 'info'
export const taskStatusColor: Record<TaskStatus, ChipColor> = { Pending: 'default', InProgress: 'info', Completed: 'success', Cancelled: 'warning' }
export const priorityColor: Record<TaskPriority, ChipColor> = { Low: 'default', Medium: 'info', High: 'warning', Critical: 'error' }
export const leaveStatusColor: Record<LeaveStatus, ChipColor> = { Pending: 'warning', Approved: 'success', Rejected: 'error' }
