import { useEffect, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Autocomplete, Button, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField } from '@mui/material'
import { employeesApi, queryKeys, tasksApi } from '../../api/endpoints'
import { toApiError } from '../../api/client'
import { TASK_PRIORITIES, type TaskDetail, type TaskStatus } from '../../api/types'
import { useNotify } from '../../hooks/useNotify'
import { applyServerErrors } from '../../utils/forms'
import { statusLabel, todayIso } from '../../utils/format'

const schema = z.object({
  title: z.string().trim().min(1, 'Title is required').max(200),
  description: z.string().max(2000),
  priority: z.enum(['Low', 'Medium', 'High', 'Critical']),
  status: z.enum(['Pending', 'InProgress', 'Completed', 'Cancelled']),
  dueDate: z.string().min(1, 'Due date is required'),
  assigneeId: z.string().nullable(),
})
type FormValues = z.infer<typeof schema>

/** Create (task undefined) or edit an existing task. Edits send the loaded version for concurrency control. */
export function TaskFormDialog({ open, task, onClose }: { open: boolean; task?: TaskDetail; onClose: () => void }) {
  const isEdit = !!task
  const queryClient = useQueryClient()
  const notify = useNotify()
  const [conflict, setConflict] = useState<string | null>(null)
  const { data: employees = [], isLoading: loadingEmployees } = useQuery({ queryKey: queryKeys.assignable, queryFn: employeesApi.assignable, enabled: open, staleTime: 5 * 60_000 })

  const { control, register, handleSubmit, reset, setError, formState: { errors, isSubmitting } } = useForm<FormValues>({ resolver: zodResolver(schema) })

  useEffect(() => {
    if (!open) return
    setConflict(null)
    reset(task
      ? { title: task.title, description: task.description, priority: task.priority, status: task.status, dueDate: task.dueDate, assigneeId: task.assigneeId }
      : { title: '', description: '', priority: 'Medium', status: 'Pending', dueDate: todayIso(), assigneeId: null })
  }, [open, task, reset])

  const save = useMutation({
    mutationFn: (v: FormValues) => {
      const body = { title: v.title, description: v.description, priority: v.priority, dueDate: v.dueDate, assigneeId: v.assigneeId }
      return task ? tasksApi.update(task.id, { ...body, status: v.status, version: task.version }) : tasksApi.create(body)
    },
    onSuccess: (saved) => {
      queryClient.setQueryData(queryKeys.task(saved.id), saved)
      void queryClient.invalidateQueries({ queryKey: queryKeys.tasks })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      notify(isEdit ? 'Task updated' : 'Task created')
      onClose()
    },
    onError: (e) => {
      const apiError = toApiError(e)
      if (apiError.isConflict) {
        setConflict(apiError.message)
        void queryClient.invalidateQueries({ queryKey: queryKeys.task(task!.id) })
      } else if (!applyServerErrors(apiError, setError)) setConflict(apiError.message)
    },
  })

  // In edit mode the status dropdown only offers the current status plus valid transitions.
  const statusOptions: TaskStatus[] = task ? [task.status, ...task.allowedTransitions] : ['Pending']

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <form onSubmit={handleSubmit((v) => save.mutateAsync(v).catch(() => undefined))} noValidate>
        <DialogTitle>{isEdit ? 'Edit task' : 'New task'}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {conflict && <Alert severity="warning">{conflict}</Alert>}
            <TextField label="Title" autoFocus {...register('title')} error={!!errors.title} helperText={errors.title?.message} />
            <TextField label="Description" multiline minRows={3} {...register('description')} error={!!errors.description} helperText={errors.description?.message} />
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
              <Controller name="priority" control={control} render={({ field }) => (
                <TextField select label="Priority" fullWidth {...field}>{TASK_PRIORITIES.map((p) => <MenuItem key={p} value={p}>{p}</MenuItem>)}</TextField>
              )} />
              <TextField label="Due date" type="date" fullWidth {...register('dueDate')} error={!!errors.dueDate} helperText={errors.dueDate?.message}
                slotProps={{ inputLabel: { shrink: true }, htmlInput: { min: isEdit ? undefined : todayIso() } }} />
            </Stack>
            {isEdit && (
              <Controller name="status" control={control} render={({ field }) => (
                <TextField select label="Status" {...field} helperText="Only valid transitions from the current status are listed.">
                  {statusOptions.map((s) => <MenuItem key={s} value={s}>{statusLabel(s)}</MenuItem>)}
                </TextField>
              )} />
            )}
            <Controller name="assigneeId" control={control} render={({ field }) => (
              <Autocomplete
                options={employees} loading={loadingEmployees}
                value={employees.find((e) => e.id === field.value) ?? null}
                onChange={(_, value) => field.onChange(value?.id ?? null)}
                getOptionLabel={(o) => o.fullName} groupBy={(o) => o.department}
                isOptionEqualToValue={(a, b) => a.id === b.id}
                renderInput={(params) => <TextField {...params} label="Assignee" placeholder="Unassigned" error={!!errors.assigneeId} helperText={errors.assigneeId?.message ?? 'Only active employees can be assigned.'} />}
              />
            )} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="contained" disabled={isSubmitting}>{isEdit ? 'Save changes' : 'Create task'}</Button>
        </DialogActions>
      </form>
    </Dialog>
  )
}
