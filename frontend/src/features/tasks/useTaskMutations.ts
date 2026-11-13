import { useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys, tasksApi } from '../../api/endpoints'
import { toApiError } from '../../api/client'
import type { PagedResult, TaskDetail, TaskListItem, TaskStatus } from '../../api/types'
import { useNotify } from '../../hooks/useNotify'

/**
 * Status change with an optimistic update: every cached task list shows the new status
 * immediately; on failure (e.g. 409 concurrency conflict) the snapshot is restored and
 * fresh data is refetched.
 */
export function useUpdateTaskStatus() {
  const queryClient = useQueryClient()
  const notify = useNotify()
  return useMutation({
    mutationFn: ({ id, status, version }: { id: number; status: TaskStatus; version: number }) => tasksApi.updateStatus(id, status, version),
    onMutate: async ({ id, status }) => {
      await queryClient.cancelQueries({ queryKey: queryKeys.tasks })
      const snapshot = queryClient.getQueriesData<PagedResult<TaskListItem> | TaskDetail>({ queryKey: queryKeys.tasks })
      queryClient.setQueriesData<PagedResult<TaskListItem> | TaskDetail>({ queryKey: queryKeys.tasks }, (old) => {
        if (!old) return old
        if ('items' in old) return { ...old, items: old.items.map((t) => (t.id === id ? { ...t, status } : t)) }
        return old.id === id ? { ...old, status } : old
      })
      return { snapshot }
    },
    onError: (error, _vars, ctx) => {
      ctx?.snapshot.forEach(([key, data]) => queryClient.setQueryData(key, data))
      notify(toApiError(error).message, 'error')
    },
    onSuccess: () => notify('Task status updated'),
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.tasks })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
  })
}

export function useDeleteTask() {
  const queryClient = useQueryClient()
  const notify = useNotify()
  return useMutation({
    mutationFn: (id: number) => tasksApi.remove(id),
    onSuccess: () => {
      notify('Task deleted')
      void queryClient.invalidateQueries({ queryKey: queryKeys.tasks })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (e) => notify(toApiError(e).message, 'error'),
  })
}
