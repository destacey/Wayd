'use client'

import {
  AppstoreOutlined,
  BuildOutlined,
  MenuOutlined,
} from '@ant-design/icons'
import { Segmented } from 'antd'
import { FC } from 'react'

export type PpmView = 'Card' | 'List' | 'Timeline'

const OPTIONS: Record<PpmView, { value: PpmView; icon: React.ReactNode }> = {
  Card: { value: 'Card', icon: <AppstoreOutlined title="Card view" /> },
  List: { value: 'List', icon: <MenuOutlined alt="List" title="List" /> },
  Timeline: {
    value: 'Timeline',
    icon: <BuildOutlined alt="Timeline" title="Timeline" />,
  },
}

export interface PpmViewSelectorProps {
  /** The views on offer, in display order. Defaults to List and Timeline. */
  views?: PpmView[]
  value: PpmView
  onChange: (view: PpmView) => void
  className?: string
}

/**
 * The icon-only List / Card / Timeline switch every PPM list page carries in
 * its grid toolbar. One definition, so the icons, titles and sizing agree
 * wherever it appears.
 */
const PpmViewSelector: FC<PpmViewSelectorProps> = ({
  views = ['List', 'Timeline'],
  value,
  onChange,
  className,
}) => (
  <Segmented
    className={className}
    options={views.map((view) => OPTIONS[view])}
    value={value}
    onChange={(next) => onChange(next as PpmView)}
    aria-label="View"
  />
)

export default PpmViewSelector
