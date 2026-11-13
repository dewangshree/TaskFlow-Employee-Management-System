import { TableCell, TableSortLabel } from '@mui/material'
import type { ListParams } from '../api/types'

export function SortableHeader({ field, label, params, onSort, align }: {
  field: string; label: string; params: ListParams; onSort: (field: string) => void; align?: 'right'
}) {
  const active = params.sortBy === field
  return (
    <TableCell align={align} sortDirection={active ? (params.sortDirection === 'Asc' ? 'asc' : 'desc') : false}>
      <TableSortLabel active={active} direction={active && params.sortDirection === 'Asc' ? 'asc' : 'desc'} onClick={() => onSort(field)}>
        {label}
      </TableSortLabel>
    </TableCell>
  )
}
