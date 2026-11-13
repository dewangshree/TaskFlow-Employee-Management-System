import { memo, useCallback, useEffect, useState } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, Box, Button, Card, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, Table, TableBody, TableCell,
  TableContainer, TableHead, TablePagination, TableRow, TextField, Tooltip, Typography,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import { leavesApi, queryKeys } from '../../api/endpoints'
import { toApiError } from '../../api/client'
import { LEAVE_STATUSES, LEAVE_TYPES, type Leave } from '../../api/types'
import { EmptyState, ErrorState, LeaveStatusChip, PageHeader, TableMessageRow, TableSkeletonRows } from '../../components/Common'
import { SearchField } from '../../components/SearchField'
import { SortableHeader } from '../../components/SortableHeader'
import { useListParams } from '../../hooks/useListParams'
import { useDocumentTitle } from '../../hooks/useDocumentTitle'
import { useNotify } from '../../hooks/useNotify'
import { applyServerErrors } from '../../utils/forms'
import { formatDate, todayIso } from '../../utils/format'

const LeaveRow = memo(function LeaveRow({ leave, admin, onReview }: { leave: Leave; admin: boolean; onReview?: (l: Leave) => void }) {
  return (
    <TableRow hover>
      {admin && <TableCell><Typography variant="body2" sx={{ fontWeight: 500 }}>{leave.employeeName}</Typography><Typography variant="caption" color="text.secondary">{leave.employeeDepartment}</Typography></TableCell>}
      <TableCell>{leave.leaveType}</TableCell>
      <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDate(leave.startDate)} – {formatDate(leave.endDate)}<Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>{leave.totalDays} day{leave.totalDays > 1 ? 's' : ''}</Typography></TableCell>
      <TableCell sx={{ maxWidth: 280 }}><Tooltip title={leave.reason}><Typography variant="body2" noWrap>{leave.reason}</Typography></Tooltip></TableCell>
      <TableCell><LeaveStatusChip status={leave.status} />
        {leave.adminComment && <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>“{leave.adminComment}”</Typography>}</TableCell>
      <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDate(leave.appliedOn)}</TableCell>
      {admin && <TableCell align="right">{leave.status === 'Pending' ? <Button size="small" variant="outlined" onClick={() => onReview?.(leave)}>Review</Button> : <Typography variant="caption" color="text.secondary">{leave.reviewedByName ? `by ${leave.reviewedByName}` : ''}</Typography>}</TableCell>}
    </TableRow>
  )
})

function LeaveTable({ scope, onReview, toolbarExtra }: { scope: 'all' | 'mine'; onReview?: (l: Leave) => void; toolbarExtra?: React.ReactNode }) {
  const admin = scope === 'all'
  const { params, dispatch, activeFilterCount } = useListParams({ sortBy: 'appliedOn', status: admin ? 'Pending' : undefined })
  const { data, isLoading, isFetching, error, refetch } = useQuery({
    queryKey: queryKeys.leaveList(scope, params),
    queryFn: () => (admin ? leavesApi.list(params) : leavesApi.mine(params)),
    placeholderData: keepPreviousData,
  })
  const onSort = useCallback((field: string) => dispatch({ type: 'sort', field }), [dispatch])
  const onSearch = useCallback((value: string) => dispatch({ type: 'search', value }), [dispatch])
  const columns = admin ? 7 : 5

  return (
    <Card variant="outlined">
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} sx={{ p: 2, alignItems: { md: 'center' } }}>
        <SearchField onSearch={onSearch} placeholder={admin ? 'Search employee or reason  ( / )' : 'Search reason  ( / )'} />
        <TextField select size="small" label="Status" value={params.status ?? ''} sx={{ minWidth: 150 }} onChange={(e) => dispatch({ type: 'filter', key: 'status', value: e.target.value })}>
          <MenuItem value="">All statuses</MenuItem>
          {LEAVE_STATUSES.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
        </TextField>
        <TextField select size="small" label="Type" value={params.leaveType ?? ''} sx={{ minWidth: 150 }} onChange={(e) => dispatch({ type: 'filter', key: 'leaveType', value: e.target.value })}>
          <MenuItem value="">All types</MenuItem>
          {LEAVE_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
        </TextField>
        {toolbarExtra}
      </Stack>
      {error && <Box sx={{ px: 2, pb: 2 }}><ErrorState error={error} onRetry={refetch} /></Box>}
      <TableContainer sx={{ opacity: isFetching && !isLoading ? 0.6 : 1 }}>
        <Table>
          <TableHead>
            <TableRow>
              {admin && <SortableHeader field="employee" label="Employee" params={params} onSort={onSort} />}
              <TableCell>Type</TableCell>
              <SortableHeader field="startDate" label="Dates" params={params} onSort={onSort} />
              <TableCell>Reason</TableCell>
              <SortableHeader field="status" label="Status" params={params} onSort={onSort} />
              <SortableHeader field="appliedOn" label="Applied" params={params} onSort={onSort} />
              {admin && <TableCell align="right">Action</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading && <TableSkeletonRows columns={columns} />}
            {data?.items.length === 0 && <TableMessageRow columns={columns}><EmptyState title={activeFilterCount || params.search ? 'No leave requests match your filters' : 'No leave requests yet'} /></TableMessageRow>}
            {data?.items.map((l) => <LeaveRow key={l.id} leave={l} admin={admin} onReview={onReview} />)}
          </TableBody>
        </Table>
      </TableContainer>
      <TablePagination component="div" count={data?.totalCount ?? 0} page={(data?.page ?? params.page) - 1} rowsPerPage={params.pageSize} rowsPerPageOptions={[10, 20, 50]}
        onPageChange={(_, p) => dispatch({ type: 'page', page: p + 1 })} onRowsPerPageChange={(e) => dispatch({ type: 'pageSize', pageSize: Number(e.target.value) })} />
    </Card>
  )
}

const applySchema = z.object({
  leaveType: z.enum(['Casual', 'Sick', 'Annual', 'Unpaid']),
  startDate: z.string().min(1, 'Required'),
  endDate: z.string().min(1, 'Required'),
  reason: z.string().trim().min(1, 'Please give a reason').max(1000),
}).refine((v) => v.endDate >= v.startDate, { path: ['endDate'], message: 'End date must be on or after start date' })
type ApplyValues = z.infer<typeof applySchema>

function ApplyLeaveDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const queryClient = useQueryClient()
  const notify = useNotify()
  const [formError, setFormError] = useState<string | null>(null)
  const { control, register, handleSubmit, reset, setError, formState: { errors, isSubmitting } } = useForm<ApplyValues>({ resolver: zodResolver(applySchema) })

  useEffect(() => {
    if (open) { setFormError(null); reset({ leaveType: 'Casual', startDate: todayIso(), endDate: todayIso(), reason: '' }) }
  }, [open, reset])

  const apply = useMutation({
    mutationFn: leavesApi.apply,
    onSuccess: () => {
      notify('Leave request submitted. Administrators have been notified.')
      void queryClient.invalidateQueries({ queryKey: queryKeys.leaves })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      onClose()
    },
    onError: (e) => { const err = toApiError(e); if (!applyServerErrors(err, setError)) setFormError(err.message) },
  })

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <form onSubmit={handleSubmit((v) => apply.mutateAsync(v).catch(() => undefined))} noValidate>
        <DialogTitle>Request leave</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {formError && <Alert severity="warning">{formError}</Alert>}
            <Controller name="leaveType" control={control} render={({ field }) => (
              <TextField select label="Leave type" {...field}>{LEAVE_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}</TextField>
            )} />
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
              <TextField label="Start date" type="date" fullWidth {...register('startDate')} error={!!errors.startDate} helperText={errors.startDate?.message} slotProps={{ inputLabel: { shrink: true } }} />
              <TextField label="End date" type="date" fullWidth {...register('endDate')} error={!!errors.endDate} helperText={errors.endDate?.message} slotProps={{ inputLabel: { shrink: true } }} />
            </Stack>
            <TextField label="Reason" multiline minRows={3} {...register('reason')} error={!!errors.reason} helperText={errors.reason?.message} />
          </Stack>
        </DialogContent>
        <DialogActions><Button onClick={onClose}>Cancel</Button><Button type="submit" variant="contained" disabled={isSubmitting}>Submit request</Button></DialogActions>
      </form>
    </Dialog>
  )
}

function ReviewLeaveDialog({ leave, onClose }: { leave: Leave | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const notify = useNotify()
  const [comment, setComment] = useState('')
  const [conflict, setConflict] = useState<string | null>(null)
  useEffect(() => { setComment(''); setConflict(null) }, [leave])

  const review = useMutation({
    mutationFn: (decision: 'Approved' | 'Rejected') => leavesApi.review(leave!.id, decision, comment, leave!.version),
    onSuccess: (updated) => {
      notify(`Leave ${updated.status.toLowerCase()}. ${updated.employeeName} will be notified.`)
      void queryClient.invalidateQueries({ queryKey: queryKeys.leaves })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      onClose()
    },
    onError: (e) => {
      const err = toApiError(e)
      setConflict(err.message)
      if (err.isConflict) void queryClient.invalidateQueries({ queryKey: queryKeys.leaves })
    },
  })

  return (
    <Dialog open={!!leave} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Review leave request</DialogTitle>
      <DialogContent>
        {leave && (
          <Stack spacing={2}>
            {conflict && <Alert severity="warning">{conflict}</Alert>}
            <Box>
              <Typography variant="subtitle1">{leave.employeeName}</Typography>
              <Typography variant="body2" color="text.secondary">{leave.employeeDepartment} · {leave.leaveType} leave · {leave.totalDays} day{leave.totalDays > 1 ? 's' : ''}</Typography>
              <Typography variant="body2" sx={{ mt: 1 }}>{formatDate(leave.startDate)} – {formatDate(leave.endDate)}</Typography>
            </Box>
            <Box sx={{ p: 2, bgcolor: 'action.hover', borderRadius: 1 }}><Typography variant="body2" sx={{ whiteSpace: 'pre-wrap' }}>{leave.reason}</Typography></Box>
            <TextField label="Comment (optional)" multiline minRows={2} value={comment} onChange={(e) => setComment(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }} />
          </Stack>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button color="error" disabled={review.isPending} onClick={() => review.mutate('Rejected')}>Reject</Button>
        <Button variant="contained" color="success" disabled={review.isPending} onClick={() => review.mutate('Approved')}>Approve</Button>
      </DialogActions>
    </Dialog>
  )
}

export function MyLeavesPage() {
  useDocumentTitle('My leave')
  const [open, setOpen] = useState(false)
  return (
    <>
      <PageHeader title="My leave" subtitle="Request time off and track decisions."
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>Request leave</Button>} />
      <LeaveTable scope="mine" />
      <ApplyLeaveDialog open={open} onClose={() => setOpen(false)} />
    </>
  )
}

export function ManageLeavesPage() {
  useDocumentTitle('Leave requests')
  const [reviewing, setReviewing] = useState<Leave | null>(null)
  const onReview = useCallback((l: Leave) => setReviewing(l), [])
  return (
    <>
      <PageHeader title="Leave requests" subtitle="Review and decide on employee leave. Pending requests are shown first." />
      <LeaveTable scope="all" onReview={onReview} />
      <ReviewLeaveDialog leave={reviewing} onClose={() => setReviewing(null)} />
    </>
  )
}
