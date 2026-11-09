'use client'

import { useLinkedEmployee } from '@/src/hooks'
import { useGetHomeContextQuery } from '@/src/store/features/user-management/me-api'

/**
 * What the caller is involved in, for the home page's default order and for
 * widgets to skip a request whose answer it already gives. Undefined while
 * loading, after a failure, and for an unlinked account, which is involved in
 * nothing and is not asked.
 *
 * Involvement is never stored, so the page's layout passes `refresh` to fetch
 * it again on every visit, showing the last answer meanwhile. Widgets leave it
 * off: they mount after the layout's answer arrived, and forcing a refetch from
 * each would ask again on every page load.
 */
export const useHomeContext = ({ refresh = false } = {}) => {
  const { hasLinkedEmployee } = useLinkedEmployee()
  const { data, isLoading, isError } = useGetHomeContextQuery(undefined, {
    skip: !hasLinkedEmployee,
    refetchOnMountOrArgChange: refresh,
  })
  return { context: isError ? undefined : data, isLoading }
}
