'use client'

import { Descriptions, Flex, Typography } from 'antd'
import type { ReactNode } from 'react'
import { gridKeymapFor, type GridRowActionSupport } from './grid-keymap'

const { Title, Text } = Typography

const PANEL_STYLE = {
  width: 400,
  maxWidth: 'calc(100vw - 48px)',
  maxHeight: 'min(70dvh, 500px)',
  overflowY: 'auto',
  overflowX: 'hidden',
  overscrollBehavior: 'contain',
  paddingRight: 4,
} as const

export interface WaydGridKeyboardHelpProps {
  /** Include the editing keys. */
  editing?: boolean
  /** Include the tree keys. */
  tree?: boolean
  /** The row actions the grid supports; their keys are listed. */
  rows?: GridRowActionSupport
  /** Grid-specific sections after the keys (what the colors mean, say). */
  children?: ReactNode
}

/**
 * The grid's keyboard help, generated from the core keymap so no grid's help
 * can drift from what its keys do.
 */
export function WaydGridKeyboardHelp({
  editing = false,
  tree = false,
  rows,
  children,
}: WaydGridKeyboardHelpProps) {
  return (
    // Focusable and named, so its scrolling list can be read by keyboard.
    <div
      style={PANEL_STYLE}
      role="region"
      aria-label="Keyboard shortcuts"
      tabIndex={0}
    >
      <Flex vertical gap="large" style={{ width: '100%' }}>
        <Title level={4}>Keyboard Shortcuts</Title>
        {gridKeymapFor({ editing, tree, rows }).map((section) => (
          <Descriptions
            key={section.title}
            size="small"
            column={1}
            styles={{ header: { marginBottom: 4 } }}
            title={section.title}
            items={section.bindings.map((binding) => ({
              key: binding.keys + binding.description,
              label: binding.keys,
              children: <Text>{binding.description}</Text>,
            }))}
          />
        ))}
        {children}
      </Flex>
    </div>
  )
}
