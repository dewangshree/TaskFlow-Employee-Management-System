import { useEffect, useRef, useState } from 'react'
import { InputAdornment, TextField } from '@mui/material'
import SearchIcon from '@mui/icons-material/Search'
import { useDebounce } from '../hooks/useDebounce'

/**
 * Local input state updates on every keystroke (instant feedback) while the parent only
 * receives the debounced value, so the API is queried once typing pauses.
 * Press "/" anywhere to focus (useRef).
 */
export function SearchField({ onSearch, placeholder = 'Search…' }: { onSearch: (value: string) => void; placeholder?: string }) {
  const [value, setValue] = useState('')
  const debounced = useDebounce(value.trim())
  const inputRef = useRef<HTMLInputElement>(null)
  const onSearchRef = useRef(onSearch)
  onSearchRef.current = onSearch

  useEffect(() => { onSearchRef.current(debounced) }, [debounced])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement
      if (e.key === '/' && !['INPUT', 'TEXTAREA'].includes(target.tagName)) {
        e.preventDefault()
        inputRef.current?.focus()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  return (
    <TextField
      size="small" value={value} onChange={(e) => setValue(e.target.value)} placeholder={placeholder} inputRef={inputRef}
      sx={{ minWidth: { sm: 280 } }}
      slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> }, htmlInput: { 'aria-label': 'Search', maxLength: 100 } }}
    />
  )
}
