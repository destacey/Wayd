'use client'

import { renderAssignedToLink } from '@/src/components/common/wayd-grid'
import useAuth from '@/src/components/contexts/auth'
import { EmployeeNavigationDto } from '@/src/services/wayd-api'
import { FC } from 'react'

/**
 * A person named on a request: a link to their page for a reader who can open employee pages, and a plain
 * name for everyone else, since most requesters cannot and a link would only lead them to a refusal.
 */
const EmployeeName: FC<{ employee: EmployeeNavigationDto | undefined }> = ({
  employee,
}) => {
  const { hasPermissionClaim } = useAuth()
  if (!employee) return null
  return hasPermissionClaim('Permissions.Employees.View') ? (
    renderAssignedToLink(employee)
  ) : (
    <>{employee.name}</>
  )
}

export default EmployeeName
