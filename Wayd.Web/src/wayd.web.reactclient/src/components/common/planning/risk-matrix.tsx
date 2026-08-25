'use client'

import { Flex, Typography } from 'antd'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import styles from './risk-matrix.module.css'

const { Text } = Typography

/** Low / Medium / High, matching the server's RiskGrade. */
const GRADES = ['Low', 'Medium', 'High'] as const

type Grade = (typeof GRADES)[number]

const gradeIndex = (name?: string) =>
  GRADES.findIndex((g) => g.toLowerCase() === name?.toLowerCase())

/**
 * The domain's own rule: exposure is Impact + Likelihood, banded at 4.
 * Kept in step with Risk.Exposure on the server — a cell coloured by a
 * different rule than the one that graded the risk would quietly disagree
 * with the exposure printed beneath it.
 */
const exposureOf = (impact: number, likelihood: number): Grade => {
  const total = impact + likelihood + 2
  if (total < 4) return 'Low'
  if (total === 4) return 'Medium'
  return 'High'
}

export interface RiskMatrixProps {
  impact?: string
  likelihood?: string
  /** Named beneath the grid. Derived server-side from the two axes. */
  exposure?: string
}

/**
 * Where a risk sits on the impact/likelihood grid.
 *
 * Three text rows say what the grading is; the grid says what it means —
 * whether the risk is one step from acceptable, and which step would move it.
 * Deliberately thumbnail-sized: a full interactive matrix belongs to a
 * register, where its job is comparing many risks against each other.
 */
const RiskMatrix = ({ impact, likelihood, exposure }: RiskMatrixProps) => {
  const impactIndex = gradeIndex(impact)
  const likelihoodIndex = gradeIndex(likelihood)

  // An unrecognised grade would place the marker wrongly rather than not at
  // all, so the grid is skipped entirely.
  if (impactIndex < 0 || likelihoodIndex < 0) return null

  return (
    <Flex vertical gap={6} className={styles.wrap}>
      <Flex gap={4}>
        <div className={styles.yAxis} aria-hidden>
          Likelihood
        </div>

        <div>
          {/* Rendered high likelihood first so the grid reads the way it is
              drawn — severity rising up and to the right. */}
          <div className={styles.grid} role="img" aria-label={ariaLabel(impact, likelihood, exposure)}>
            {[2, 1, 0].map((row) =>
              [0, 1, 2].map((col) => {
                const isRisk = row === likelihoodIndex && col === impactIndex
                const band = exposureOf(col, row).toLowerCase()
                return (
                  <div
                    key={`${row}-${col}`}
                    className={`${styles.cell} ${styles[band]} ${isRisk ? styles.marked : ''}`}
                  >
                    {isRisk && <span className={styles.marker} aria-hidden />}
                  </div>
                )
              }),
            )}
          </div>

          <Flex className={styles.xLabels} aria-hidden>
            {GRADES.map((g) => (
              <span key={g}>{g}</span>
            ))}
          </Flex>
          <div className={styles.xAxis} aria-hidden>
            Impact
          </div>
        </div>
      </Flex>

      {exposure && (
        <WaydTooltip title={`Impact ${impact} · Likelihood ${likelihood}`}>
          <Text className={styles.exposure}>
            Exposure <Text strong>{exposure}</Text>
          </Text>
        </WaydTooltip>
      )}
    </Flex>
  )
}

const ariaLabel = (
  impact?: string,
  likelihood?: string,
  exposure?: string,
) =>
  `Risk matrix. Impact ${impact}, likelihood ${likelihood}, giving ${exposure} exposure.`

export default RiskMatrix
