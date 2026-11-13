import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Card, CardContent, CardHeader, Grid, Stack, TextField } from '@mui/material'
import { profileApi, queryKeys } from '../../api/endpoints'
import { toApiError } from '../../api/client'
import { useAuth } from '../../auth/AuthContext'
import { ErrorState, PageHeader, StatCard } from '../../components/Common'
import { useDocumentTitle } from '../../hooks/useDocumentTitle'
import { useNotify } from '../../hooks/useNotify'
import { applyServerErrors } from '../../utils/forms'
import { formatDate } from '../../utils/format'
import { passwordSchema } from '../auth/RegisterPage'

const profileSchema = z.object({
  firstName: z.string().trim().min(1, 'Required').max(50),
  lastName: z.string().trim().min(1, 'Required').max(50),
  email: z.string().email('Enter a valid email'),
  department: z.string().trim().min(1, 'Required').max(100),
})
const passwordFormSchema = z.object({ currentPassword: z.string().min(1, 'Required'), newPassword: passwordSchema, confirmPassword: z.string() })
  .refine((v) => v.newPassword === v.confirmPassword, { path: ['confirmPassword'], message: 'Passwords do not match' })

export default function ProfilePage() {
  useDocumentTitle('Profile')
  const { user, signIn } = useAuth()
  const notify = useNotify()
  const queryClient = useQueryClient()
  const [profileError, setProfileError] = useState<string | null>(null)
  const { data: profile, isLoading, error, refetch } = useQuery({ queryKey: queryKeys.profile, queryFn: profileApi.get })

  const form = useForm<z.infer<typeof profileSchema>>({ resolver: zodResolver(profileSchema) })
  const pwd = useForm<z.infer<typeof passwordFormSchema>>({ resolver: zodResolver(passwordFormSchema), defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' } })

  useEffect(() => {
    if (profile) form.reset({ firstName: profile.firstName, lastName: profile.lastName, email: profile.email, department: profile.department })
  }, [profile, form])

  const update = useMutation({
    mutationFn: profileApi.update,
    onSuccess: (p) => {
      queryClient.setQueryData(queryKeys.profile, p)
      if (user) signIn({ ...user, firstName: p.firstName, lastName: p.lastName, fullName: `${p.firstName} ${p.lastName}`, email: p.email, department: p.department })
      notify('Profile updated')
    },
    onError: (e) => { const err = toApiError(e); if (!applyServerErrors(err, form.setError)) setProfileError(err.message) },
  })

  const changePassword = useMutation({
    mutationFn: profileApi.changePassword,
    onSuccess: () => { pwd.reset(); notify('Password changed') },
    onError: (e) => { const err = toApiError(e); if (!applyServerErrors(err, pwd.setError)) notify(err.message, 'error') },
  })

  if (error) return <ErrorState error={error} onRetry={refetch} />
  const fe = form.formState.errors
  const pe = pwd.formState.errors

  return (
    <>
      <PageHeader title="Profile" subtitle={profile ? `${profile.role} · joined ${formatDate(profile.joinDate)}` : undefined} />
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={{ xs: 12, sm: 4 }}><StatCard label="Assigned tasks" value={profile?.assignedTasks ?? 0} loading={isLoading} /></Grid>
        <Grid size={{ xs: 12, sm: 4 }}><StatCard label="Completed tasks" value={profile?.completedTasks ?? 0} loading={isLoading} /></Grid>
        <Grid size={{ xs: 12, sm: 4 }}><StatCard label="Pending leave requests" value={profile?.pendingLeaveRequests ?? 0} loading={isLoading} /></Grid>
      </Grid>
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 7 }}>
          <Card variant="outlined">
            <CardHeader title="Personal details" slotProps={{ title: { variant: 'subtitle1' } }} />
            <CardContent>
              <Stack component="form" spacing={2} noValidate onSubmit={form.handleSubmit((v) => { setProfileError(null); return update.mutateAsync(v).catch(() => undefined) })}>
                {profileError && <Alert severity="error">{profileError}</Alert>}
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
                  <TextField label="First name" fullWidth {...form.register('firstName')} error={!!fe.firstName} helperText={fe.firstName?.message} slotProps={{ inputLabel: { shrink: true } }} />
                  <TextField label="Last name" fullWidth {...form.register('lastName')} error={!!fe.lastName} helperText={fe.lastName?.message} slotProps={{ inputLabel: { shrink: true } }} />
                </Stack>
                <TextField label="Email" type="email" {...form.register('email')} error={!!fe.email} helperText={fe.email?.message ?? 'Your email is also your sign-in name.'} slotProps={{ inputLabel: { shrink: true } }} />
                <TextField label="Department" {...form.register('department')} error={!!fe.department} helperText={fe.department?.message} slotProps={{ inputLabel: { shrink: true } }} />
                <Stack direction="row" sx={{ justifyContent: 'flex-end' }}>
                  <Button type="submit" variant="contained" disabled={!form.formState.isDirty || form.formState.isSubmitting}>Save changes</Button>
                </Stack>
              </Stack>
            </CardContent>
          </Card>
        </Grid>
        <Grid size={{ xs: 12, md: 5 }}>
          <Card variant="outlined">
            <CardHeader title="Change password" slotProps={{ title: { variant: 'subtitle1' } }} />
            <CardContent>
              <Stack component="form" spacing={2} noValidate onSubmit={pwd.handleSubmit((v) => changePassword.mutateAsync(v).catch(() => undefined))}>
                <TextField label="Current password" type="password" autoComplete="current-password" {...pwd.register('currentPassword')} error={!!pe.currentPassword} helperText={pe.currentPassword?.message} />
                <TextField label="New password" type="password" autoComplete="new-password" {...pwd.register('newPassword')} error={!!pe.newPassword} helperText={pe.newPassword?.message} />
                <TextField label="Confirm new password" type="password" autoComplete="new-password" {...pwd.register('confirmPassword')} error={!!pe.confirmPassword} helperText={pe.confirmPassword?.message} />
                <Stack direction="row" sx={{ justifyContent: 'flex-end' }}>
                  <Button type="submit" variant="outlined" disabled={pwd.formState.isSubmitting}>Update password</Button>
                </Stack>
              </Stack>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </>
  )
}
