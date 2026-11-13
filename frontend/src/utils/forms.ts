import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import type { ApiError } from '../api/client'

/** Maps a 400 ValidationProblemDetails onto react-hook-form fields. Returns true if any were applied. */
export function applyServerErrors<T extends FieldValues>(error: ApiError, setError: UseFormSetError<T>): boolean {
  const entries = Object.entries(error.fieldErrors).filter(([k]) => k)
  entries.forEach(([field, messages]) => setError(field as Path<T>, { type: 'server', message: messages.join(' ') }))
  return entries.length > 0
}
