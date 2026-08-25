'use client'

import { Flex, Skeleton } from 'antd'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import { useGetRiskCategoriesQuery } from '@/src/store/features/planning/risks-api'
import styles from './risk-roam.module.css'

export interface RiskRoamProps {
  /** The chosen category's name, e.g. "Owned". */
  category?: string
}

/**
 * The risk's ROAM category, shown against the three it was chosen over.
 *
 * ROAM is a decision a team makes together — Resolved, Owned, Accepted or
 * Mitigated — so the alternatives are part of what the answer means. A lone
 * "Owned" does not say the risk was not resolvable, and few readers recall
 * how Accepted differs from Mitigated; each option carries its definition
 * from the server as a tooltip.
 */
const RiskRoam = ({ category }: RiskRoamProps) => {
  const { data: categories, isLoading } = useGetRiskCategoriesQuery()

  if (isLoading) {
    return <Skeleton.Input active size="small" className={styles.skeleton} />
  }

  if (!categories?.length) return null

  return (
    // No "ROAM" label: the four options spell it, and naming the acronym
    // above them says nothing the track does not.
    <Flex className={styles.track} role="list" aria-label="ROAM category">
      {categories.map((option) => {
        const isSelected = option.name.toLowerCase() === category?.toLowerCase()

        return (
          <WaydTooltip key={option.id} title={option.description}>
            <div
              role="listitem"
              aria-current={isSelected ? 'true' : undefined}
              className={`${styles.option} ${isSelected ? styles.selected : ''}`}
            >
              {option.name}
            </div>
          </WaydTooltip>
        )
      })}
    </Flex>
  )
}

export default RiskRoam
