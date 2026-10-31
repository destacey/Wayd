'use client'

import { useGetTeamProductsQuery } from '@/src/store/features/product-management/products-api'
import ProductTeamLinksGrid from './product-team-links-grid'

export interface TeamProductsProps {
  teamId: string
}

/**
 * The products a team or team of teams is linked to, for a section on the team's page.
 *
 * Fetches its own links, so a team page loads them only when the section is opened. A team of teams lists
 * only its own links, not those of the teams within it.
 */
const TeamProducts = ({ teamId }: TeamProductsProps) => {
  const { data, isLoading, refetch } = useGetTeamProductsQuery(teamId)

  return (
    <ProductTeamLinksGrid
      view={{ kind: 'team' }}
      links={data}
      isLoading={isLoading}
      refetch={refetch}
    />
  )
}

export default TeamProducts
