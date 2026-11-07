'use client'

import { Card, Switch, Typography } from 'antd'
import { useId } from 'react'
import type { FeatureFlagDto } from '@/src/services/wayd-api'
import type { FeatureFlagModuleGroup } from './group-feature-flags'
import styles from './feature-flag-module-card.module.css'

const { Text } = Typography

export interface FeatureFlagModuleCardProps {
  group: FeatureFlagModuleGroup
  /** Whether the viewer may turn flags on and off. */
  canUpdate: boolean
  onToggle: (flag: FeatureFlagDto, isEnabled: boolean) => void
}

/**
 * One module's flags. A module that has its own switch shows it in the
 * header; while it is off, the module's other flags do nothing, so their
 * switches are disabled.
 */
const FeatureFlagModuleCard = ({
  group,
  canUpdate,
  onToggle,
}: FeatureFlagModuleCardProps) => {
  const { module, moduleSwitch, flags } = group
  const moduleOff = !!moduleSwitch && !moduleSwitch.isEnabled
  const enabledCount = flags.filter((f) => f.isEnabled).length
  const moduleOffNoteId = useId()

  const title = (
    <div className={styles.titleBlock}>
      <span>{module.name}</span>
      <span className={styles.moduleDescription}>
        {moduleSwitch?.description ?? module.description}
      </span>
      {moduleSwitch && (
        <Text code className={styles.key}>
          {moduleSwitch.name}
        </Text>
      )}
    </div>
  )

  const extra = moduleSwitch ? (
    <Switch
      checked={moduleSwitch.isEnabled}
      disabled={!canUpdate}
      aria-label={`${module.name} module`}
      onChange={(checked) => onToggle(moduleSwitch, checked)}
    />
  ) : (
    <Text type="secondary">
      {enabledCount} of {flags.length} on
    </Text>
  )

  return (
    <Card
      title={title}
      extra={extra}
      size="small"
      styles={{ body: { padding: 0 } }}
    >
      {moduleOff && flags.length > 0 && (
        <div id={moduleOffNoteId} className={styles.moduleOffNote}>
          Turn on {module.name} to use these features.
        </div>
      )}
      {flags.map((flag) => (
        <div key={flag.id} className={styles.row}>
          <div className={styles.rowText}>
            <div className={styles.rowHeading}>
              <Text strong>{flag.displayName}</Text>
              <Text code className={styles.key}>
                {flag.name}
              </Text>
            </div>
            {flag.description && (
              <span className={styles.description}>{flag.description}</span>
            )}
          </div>
          <Switch
            checked={flag.isEnabled}
            disabled={!canUpdate || moduleOff}
            aria-label={flag.displayName}
            aria-describedby={moduleOff ? moduleOffNoteId : undefined}
            onChange={(checked) => onToggle(flag, checked)}
          />
        </div>
      ))}
    </Card>
  )
}

export default FeatureFlagModuleCard
