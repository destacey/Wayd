'use client'

import { Alert } from 'antd'
import { FC } from 'react'
import { useLinkedEmployee } from '@/src/hooks'

export interface UnlinkedEmployeeAlertProps {
  /**
   * What the user cannot see or do without a link, phrased to follow "so". For example
   * "your assigned work can't be determined" or "creating a roadmap is unavailable".
   */
  consequence: string
  /** Hides the alert unless this is true — e.g. only warn users who hold the relevant permission. */
  when?: boolean
}

/**
 * Explains that the signed-in account has no employee record, and what that costs them.
 *
 * Personal views ("My Projects", assigned risks) are keyed on the employee, not the user account, so
 * an unlinked account correctly resolves to nothing. Without this the page just renders empty, which
 * reads as a broken dashboard rather than an account that needs linking.
 *
 * Renders nothing when the account is linked.
 */
const UnlinkedEmployeeAlert: FC<UnlinkedEmployeeAlertProps> = ({
  consequence,
  when = true,
}) => {
  const { hasLinkedEmployee } = useLinkedEmployee()

  if (hasLinkedEmployee || !when) return null

  return (
    <Alert
      title="Your account isn't linked to an employee record"
      description={`${consequence} Ask an administrator to link your account.`}
      type="info"
      showIcon
      style={{ marginBottom: 16 }}
    />
  )
}

export default UnlinkedEmployeeAlert
