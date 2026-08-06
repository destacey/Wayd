import useAuth from '@/src/components/contexts/auth'

interface UseLinkedEmployeeResult {
  /** The employee this account acts as, or null when the account has no link. */
  employeeId: string | null
  /** Whether the account is linked to an employee record. */
  hasLinkedEmployee: boolean
}

/**
 * Hook for the current user's employee link.
 *
 * Permissions say what an account may do; the employee link says who it acts *as*. Actions that
 * record an actor — creating a roadmap or risk, recording a health check or score — need both, and
 * the API rejects a request from an unlinked account with 403 regardless of permission. Gate those
 * controls on `hasLinkedEmployee` alongside the permission check so an unlinked user is not offered
 * an action that cannot succeed.
 *
 * Read-only views should NOT use this to hide themselves: they degrade to public-visibility content
 * for unlinked users rather than failing.
 *
 * @example
 * const { hasPermissionClaim } = useAuth()
 * const { hasLinkedEmployee } = useLinkedEmployee()
 * const canCreateRoadmap =
 *   hasPermissionClaim('Permissions.Roadmaps.Create') && hasLinkedEmployee
 */
export function useLinkedEmployee(): UseLinkedEmployeeResult {
  const { user } = useAuth()
  const employeeId = user?.employeeId ?? null

  return { employeeId, hasLinkedEmployee: employeeId !== null }
}
