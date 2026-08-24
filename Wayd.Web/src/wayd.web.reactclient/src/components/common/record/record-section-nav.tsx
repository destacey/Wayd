'use client'

import { RecordLayoutConstants } from '@/src/config/theme/theme-constants'
import { Flex, Select, theme, Typography } from 'antd'
import styles from './record-layout.module.css'
import { RecordSection } from './types'

const { Text } = Typography

interface RecordSectionNavProps {
  sections: RecordSection[]
  reports: RecordSection[]
  activeSection: string
  onChange: (id: string) => void
  /** Renders a Select instead of the rail. Set below the `md` breakpoint. */
  compact: boolean
}

const RailItem = ({
  section,
  isActive,
  onChange,
}: {
  section: RecordSection
  isActive: boolean
  onChange: (id: string) => void
}) => {
  const { token } = theme.useToken()

  return (
    <div
      role="tab"
      aria-selected={isActive}
      tabIndex={0}
      className={[styles.railItem, isActive && styles.railItemActive]
        .filter(Boolean)
        .join(' ')}
      onClick={() => onChange(section.id)}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault()
          onChange(section.id)
        }
      }}
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: token.marginXS,
        // 44px minimum touch target, per the responsive rules.
        minHeight: 44,
        padding: `${token.paddingXXS}px ${token.padding}px`,
        borderLeft: `2px solid ${isActive ? token.colorPrimary : 'transparent'}`,
        background: isActive ? token.colorPrimaryBg : undefined,
        color: isActive ? token.colorPrimaryText : token.colorText,
        fontWeight: isActive ? 500 : undefined,
      }}
    >
      <span style={{ flexGrow: 1 }}>{section.label}</span>
      {section.count !== undefined && (
        <Text type="secondary" style={{ fontSize: token.fontSizeSM }}>
          {section.count}
        </Text>
      )}
    </div>
  )
}

/**
 * Section navigation for a record page — a vertical rail, or a Select on
 * small screens.
 *
 * A rail rather than tabs because tabs stop scaling: the team page alone
 * carries seven sections plus two reports, which a horizontal strip cannot
 * hold without overflow. The rail also has room for counts.
 */
const RecordSectionNav = ({
  sections,
  reports,
  activeSection,
  onChange,
  compact,
}: RecordSectionNavProps) => {
  const { token } = theme.useToken()

  if (compact) {
    const options = [
      {
        label: 'Sections',
        options: sections.map((s) => ({
          value: s.id,
          label:
            s.count !== undefined ? `${s.label} (${s.count})` : s.label,
        })),
      },
      ...(reports.length > 0
        ? [
            {
              label: 'Reports',
              options: reports.map((r) => ({ value: r.id, label: r.label })),
            },
          ]
        : []),
    ]

    return (
      <Select
        value={activeSection}
        onChange={onChange}
        options={options}
        style={{ width: '100%' }}
        size="large"
        aria-label="Section"
      />
    )
  }

  return (
    <Flex
      vertical
      gap={2}
      role="tablist"
      aria-orientation="vertical"
      style={{
        width: RecordLayoutConstants.SECTION_RAIL_WIDTH,
        flexShrink: 0,
        paddingTop: token.padding,
        paddingBottom: token.padding,
        borderRight: `1px solid ${token.colorBorderSecondary}`,
        background: token.colorBgContainer,
      }}
    >
      {sections.map((s) => (
        <RailItem
          key={s.id}
          section={s}
          isActive={s.id === activeSection}
          onChange={onChange}
        />
      ))}

      {reports.length > 0 && (
        <>
          <div
            style={{
              height: 1,
              background: token.colorBorderSecondary,
              margin: `${token.marginXS}px ${token.margin}px`,
            }}
          />
          <Text
            type="secondary"
            style={{
              fontSize: token.fontSizeSM,
              fontWeight: 600,
              letterSpacing: 0.4,
              padding: `0 ${token.padding}px ${token.paddingXXS}px`,
            }}
          >
            REPORTS
          </Text>
          {reports.map((r) => (
            <RailItem
              key={r.id}
              section={r}
              isActive={r.id === activeSection}
              onChange={onChange}
            />
          ))}
        </>
      )}
    </Flex>
  )
}

export default RecordSectionNav
