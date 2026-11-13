import { memo, type ReactNode } from 'react'
import { Alert, Box, Button, Card, CardContent, Chip, Skeleton, Stack, TableCell, TableRow, Typography } from '@mui/material'
import InboxOutlined from '@mui/icons-material/InboxOutlined'
import type { LeaveStatus, TaskPriority, TaskStatus } from '../api/types'
import { leaveStatusColor, priorityColor, statusLabel, taskStatusColor } from '../utils/format'
import { toApiError } from '../api/client'

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: string; actions?: ReactNode }) {
  return (
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 3, alignItems: { sm: 'center' }, justifyContent: 'space-between' }}>
      <Box>
        <Typography variant="h5" component="h1">{title}</Typography>
        {subtitle && <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>{subtitle}</Typography>}
      </Box>
      {actions && <Stack direction="row" spacing={1}>{actions}</Stack>}
    </Stack>
  )
}

export const TaskStatusChip = memo(({ status }: { status: TaskStatus }) =>
  <Chip size="small" label={statusLabel(status)} color={taskStatusColor[status]} variant={status === 'Pending' ? 'outlined' : 'filled'} />)

export const PriorityChip = memo(({ priority }: { priority: TaskPriority }) =>
  <Chip size="small" label={priority} color={priorityColor[priority]} variant="outlined" />)

export const LeaveStatusChip = memo(({ status }: { status: LeaveStatus }) =>
  <Chip size="small" label={status} color={leaveStatusColor[status]} variant="outlined" />)

export function EmptyState({ title, description, action }: { title: string; description?: string; action?: ReactNode }) {
  return (
    <Stack spacing={1} sx={{ alignItems: 'center', py: 6, px: 2, textAlign: 'center' }}>
      <InboxOutlined sx={{ fontSize: 40, color: 'text.disabled' }} />
      <Typography variant="subtitle1">{title}</Typography>
      {description && <Typography variant="body2" color="text.secondary">{description}</Typography>}
      {action}
    </Stack>
  )
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const apiError = toApiError(error)
  return (
    <Alert severity="error" action={onRetry && <Button color="inherit" size="small" onClick={onRetry}>Retry</Button>}>
      {apiError.message}
      {apiError.problem.correlationId && <Typography variant="caption" component="div">Reference: {apiError.problem.correlationId}</Typography>}
    </Alert>
  )
}

export function TableSkeletonRows({ rows = 5, columns }: { rows?: number; columns: number }) {
  return Array.from({ length: rows }, (_, r) => (
    <TableRow key={r}>{Array.from({ length: columns }, (_, c) => <TableCell key={c}><Skeleton variant="text" /></TableCell>)}</TableRow>
  ))
}

export function TableMessageRow({ columns, children }: { columns: number; children: ReactNode }) {
  return <TableRow><TableCell colSpan={columns} sx={{ borderBottom: 0 }}>{children}</TableCell></TableRow>
}

export function StatCard({ label, value, hint, loading }: { label: string; value: number | string; hint?: string; loading?: boolean }) {
  return (
    <Card variant="outlined" sx={{ height: '100%' }}>
      <CardContent>
        <Typography variant="body2" color="text.secondary">{label}</Typography>
        {loading ? <Skeleton width={60} height={40} /> : <Typography variant="h4" sx={{ mt: 0.5, fontWeight: 600 }}>{value}</Typography>}
        {hint && <Typography variant="caption" color="text.secondary">{hint}</Typography>}
      </CardContent>
    </Card>
  )
}
