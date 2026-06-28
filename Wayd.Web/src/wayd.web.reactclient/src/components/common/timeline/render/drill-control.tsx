'use client'

// timeline/render/drill-control.tsx — drill-through level -/+ buttons.
// Collapse (-) reduces the expand level (fold deeper groups onto parent lanes);
// expand (+) increases it. Lives in the toolbar's left slot. Mirrors the legacy
// roadmap timeline's drill control.

import { Button } from 'antd'
import { MinusSquareOutlined, PlusSquareOutlined } from '@ant-design/icons'
import WaydTooltip from '@/src/components/common/wayd-tooltip'

export interface DrillControlProps {
  level: number
  /** Highest meaningful level (maxDepth + 1 = fully expanded). */
  maxLevel: number
  onChange: (level: number) => void
}

// Renders as a fragment so the toolbar's flex `gap` spaces these buttons the
// same as every other toolbar icon (consistent with the tree-grid toolbar
// pattern — CSS gap for layout, not antd Space).
const DrillControl = ({ level, maxLevel, onChange }: DrillControlProps) => {
  // Nothing to drill when there's no nesting.
  if (maxLevel <= 1) return null

  return (
    <>
      <WaydTooltip title="Collapse one level">
        <Button
          type="text"
          shape="circle"
          icon={<MinusSquareOutlined />}
          disabled={level <= 1}
          onClick={() => onChange(level - 1)}
        />
      </WaydTooltip>
      <WaydTooltip title="Expand one level">
        <Button
          type="text"
          shape="circle"
          icon={<PlusSquareOutlined />}
          disabled={level >= maxLevel}
          onClick={() => onChange(level + 1)}
        />
      </WaydTooltip>
    </>
  )
}

export default DrillControl
