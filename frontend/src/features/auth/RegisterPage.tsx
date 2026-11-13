import { useState } from 'react'
import { Link as RouterLink, useNavigate } from 'react-router'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Alert, Button, Link, Stack, TextField, Typography } from '@mui/material'
import { authApi } from '../../api/endpoints'
import { toApiError } from '../../api/client'
import { useAuth } from '../../auth/AuthContext'
import { useDocumentTitle } from '../../hooks/useDocumentTitle'
import { applyServerErrors } from '../../utils/forms'
import { AuthLayout } from './AuthLayout'

// Mirrors the server's Identity password policy so users get immediate feedback.
export const passwordSchema = z.string().min(6, 'At least 6 characters').max(100)
  .regex(/[a-z]/, 'Include a lowercase letter').regex(/[A-Z]/, 'Include an uppercase letter')
  .regex(/\d/, 'Include a digit').regex(/[^a-zA-Z0-9]/, 'Include a symbol')

const schema = z.object({
  firstName: z.string().trim().min(1, 'Required').max(50),
  lastName: z.string().trim().min(1, 'Required').max(50),
  email: z.string().email('Enter a valid email address'),
  department: z.string().trim().min(1, 'Required').max(100),
  password: passwordSchema,
  confirmPassword: z.string(),
}).refine((v) => v.password === v.confirmPassword, { path: ['confirmPassword'], message: 'Passwords do not match' })
type FormValues = z.infer<typeof schema>

export default function RegisterPage() {
  useDocumentTitle('Create account')
  const { signIn } = useAuth()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)
  const { register, handleSubmit, setError: setFieldError, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(schema), defaultValues: { firstName: '', lastName: '', email: '', department: '', password: '', confirmPassword: '' },
  })

  const onSubmit = async (values: FormValues) => {
    setError(null)
    try {
      signIn(await authApi.register(values))
      navigate('/', { replace: true })
    } catch (e) {
      const apiError = toApiError(e)
      if (!applyServerErrors(apiError, setFieldError)) setError(apiError.message)
    }
  }

  return (
    <AuthLayout title="Create your account" subtitle="New accounts are created with the Employee role.">
      <Stack component="form" spacing={2} onSubmit={handleSubmit(onSubmit)} noValidate>
        {error && <Alert severity="error">{error}</Alert>}
        <Stack direction="row" spacing={2}>
          <TextField label="First name" fullWidth autoFocus {...register('firstName')} error={!!errors.firstName} helperText={errors.firstName?.message} />
          <TextField label="Last name" fullWidth {...register('lastName')} error={!!errors.lastName} helperText={errors.lastName?.message} />
        </Stack>
        <TextField label="Work email" type="email" autoComplete="email" {...register('email')} error={!!errors.email} helperText={errors.email?.message} />
        <TextField label="Department" {...register('department')} error={!!errors.department} helperText={errors.department?.message} />
        <TextField label="Password" type="password" autoComplete="new-password" {...register('password')} error={!!errors.password} helperText={errors.password?.message} />
        <TextField label="Confirm password" type="password" autoComplete="new-password" {...register('confirmPassword')} error={!!errors.confirmPassword} helperText={errors.confirmPassword?.message} />
        <Button type="submit" variant="contained" size="large" disabled={isSubmitting}>{isSubmitting ? 'Creating account…' : 'Create account'}</Button>
        <Typography variant="body2" color="text.secondary" align="center">
          Already registered? <Link component={RouterLink} to="/login">Sign in</Link>
        </Typography>
      </Stack>
    </AuthLayout>
  )
}
