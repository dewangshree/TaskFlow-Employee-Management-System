import { useMemo, useReducer } from 'react'
import type { ListParams, SortDirection } from '../api/types'

type Action =
  | { type: 'search'; value: string }
  | { type: 'filter'; key: string; value: string | boolean | undefined }
  | { type: 'sort'; field: string }
  | { type: 'page'; page: number }
  | { type: 'pageSize'; pageSize: number }
  | { type: 'reset' }

/**
 * Table state machine for server-side lists. A reducer keeps the coupled rules in one
 * place: changing search/filter/sort/page size always resets to page 1, and clicking the
 * active sort column toggles its direction.
 */
function reducer(initial: ListParams) {
  return (state: ListParams, action: Action): ListParams => {
    switch (action.type) {
      case 'search': return { ...state, search: action.value || undefined, page: 1 }
      case 'filter': return { ...state, [action.key]: action.value === '' ? undefined : action.value, page: 1 }
      case 'sort': {
        const direction: SortDirection = state.sortBy === action.field && state.sortDirection === 'Asc' ? 'Desc' : 'Asc'
        return { ...state, sortBy: action.field, sortDirection: direction, page: 1 }
      }
      case 'page': return { ...state, page: action.page }
      case 'pageSize': return { ...state, pageSize: action.pageSize, page: 1 }
      case 'reset': return initial
    }
  }
}

export function useListParams(initial: Partial<ListParams> = {}) {
  const initialState = useMemo<ListParams>(() => ({ page: 1, pageSize: 10, sortDirection: 'Desc', ...initial }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [])
  const reduce = useMemo(() => reducer(initialState), [initialState])
  const [params, dispatch] = useReducer(reduce, initialState)
  const activeFilterCount = Object.entries(params)
    .filter(([k, v]) => !['page', 'pageSize', 'sortBy', 'sortDirection', 'search'].includes(k) && v !== undefined).length
  return { params, dispatch, activeFilterCount }
}
