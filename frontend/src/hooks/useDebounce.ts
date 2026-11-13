import { useEffect, useState } from 'react'

/** Delays propagating a fast-changing value (search input) so the API is called once typing pauses. */
export function useDebounce<T>(value: T, delayMs = 350): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), delayMs)
    return () => window.clearTimeout(timer) // cancel on every keystroke
  }, [value, delayMs])
  return debounced
}
