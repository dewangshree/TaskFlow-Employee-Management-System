import { useEffect, useRef, useState } from 'react'
import { Link as RouterLink, useLocation, useNavigate } from 'react-router'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Alert, Button, Checkbox, FormControlLabel, Link, Stack, TextField, Typography } from '@mui/material'
import { authApi } from '../../api/endpoints'
import { toApiError } from '../../api/client'
import { useAuth } from '../../auth/AuthContext'
import { useDocumentTitle } from '../../hooks/useDocumentTitle'
import { AuthLayout } from './AuthLayout'

const schema = z.object({
  email: z.string().email('Enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
  rememberMe: z.boolean(),
})
type FormValues = z.infer<typeof schema>

export default function LoginPage() {
  useDocumentTitle('Sign in')
  const { signIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [error, setError] = useState<string | null>(null)
  const passwordRef = useRef<HTMLInputElement | null>(null)
  const { register, handleSubmit, formState: { errors, isSubmitting }, setValue } = useForm<FormValues>({
    resolver: zodResolver(schema), defaultValues: { email: '', password: '', rememberMe: false },
  })

  // After a failed attempt, clear and refocus the password field.
  useEffect(() => { if (error) passwordRef.current?.focus() }, [error])

  const onSubmit = async (values: FormValues) => {
    setError(null)
    try {
      const user = await authApi.login(values)
      signIn(user)
      navigate((location.state as { from?: string } | null)?.from ?? '/', { replace: true })
    } catch (e) {
      const apiError = toApiError(e)
      setError(apiError.status === 429 ? apiError.message : apiError.problem.detail ?? 'Sign in failed.')
      setValue('password', '')
    }
  }

  const { ref: passwordFieldRef, ...passwordField } = register('password')

  return (
    <AuthLayout title="Sign in" subtitle="Use your work account to continue.">
      <Stack component="form" spacing={2} onSubmit={handleSubmit(onSubmit)} noValidate>
        {error && <Alert severity="error">{error}</Alert>}
        <TextField label="Email" type="email" autoComplete="email" autoFocus {...register('email')} error={!!errors.email} helperText={errors.email?.message} />
        <TextField label="Password" type="password" autoComplete="current-password" {...passwordField}
          inputRef={(el: HTMLInputElement | null) => { passwordFieldRef(el); passwordRef.current = el }}
          error={!!errors.password} helperText={errors.password?.message} />
        <FormControlLabel control={<Checkbox {...register('rememberMe')} />} label="Keep me signed in" />
        <Button type="submit" variant="contained" size="large" disabled={isSubmitting}>{isSubmitting ? 'Signing in…' : 'Sign in'}</Button>
        <Typography variant="body2" color="text.secondary" align="center">
          New employee? <Link component={RouterLink} to="/register">Create an account</Link>
        </Typography>
      </Stack>
    </AuthLayout>
  )
}
