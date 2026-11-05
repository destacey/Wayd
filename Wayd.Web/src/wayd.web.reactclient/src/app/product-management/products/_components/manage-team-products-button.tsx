'use client'

import useAuth from '@/src/components/contexts/auth'
import { useGetTeamProductsQuery } from '@/src/store/features/product-management/products-api'
import { Button } from 'antd'
import { useState } from 'react'
import ManageTeamProductsForm from './manage-team-products-form'

export interface ManageTeamProductsButtonProps {
  teamId: string
  teamName: string
}

/**
 * Manage Products for a team page's section header, with the dialog it opens. Renders nothing without permission
 * to update products.
 *
 * Reads the team's products from the same query as the section, so the cache answers it rather than a second
 * request.
 */
const ManageTeamProductsButton = ({
  teamId,
  teamName,
}: ManageTeamProductsButtonProps) => {
  const [isManaging, setIsManaging] = useState(false)
  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.Products.Update')

  const { data, isLoading } = useGetTeamProductsQuery(teamId, {
    skip: !canUpdate,
  })

  if (!canUpdate) return null

  return (
    <>
      <Button disabled={isLoading} onClick={() => setIsManaging(true)}>
        Manage Products
      </Button>

      {isManaging && (
        <ManageTeamProductsForm
          teamId={teamId}
          teamName={teamName}
          currentProductIds={(data ?? []).map((link) => link.product.id)}
          onFormComplete={() => setIsManaging(false)}
          onFormCancel={() => setIsManaging(false)}
        />
      )}
    </>
  )
}

export default ManageTeamProductsButton
