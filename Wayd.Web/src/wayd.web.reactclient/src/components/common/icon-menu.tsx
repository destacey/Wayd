'use client'

import { Button, Dropdown } from 'antd'
import type { MenuProps } from 'antd'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import { FC, ReactNode } from 'react'
import useTheme from '../contexts/theme'

export interface IconMenuOption {
  label: string
  value: string | number
  extra?: string
}

export interface IconMenuProps {
  icon: ReactNode
  items: IconMenuOption[]
  tooltip?: string
  selectedKeys?: string[]
  maxHeight?: number
  /** Render the trigger even when `items` is empty (e.g. data is lazily loaded on open). */
  alwaysRender?: boolean
  onChange?: (value: string | number) => void
  onOpenChange?: (open: boolean) => void
}

const IconMenu: FC<IconMenuProps> = ({
  icon,
  items,
  tooltip,
  selectedKeys,
  maxHeight = 400,
  alwaysRender = false,
  onChange,
  onOpenChange,
}: IconMenuProps) => {
  const { token } = useTheme()

  const menuItems: MenuProps['items'] =
    items?.map((item) => ({
      key: item.value.toString(),
      label: item.label,
      extra: item.extra,
    })) || []

  const handleMenuClick: MenuProps['onClick'] = (info) => {
    const selectedItem = items?.find(
      (item) => item.value.toString() === info.key,
    )
    if (selectedItem && onChange) {
      onChange(selectedItem.value)
    }
  }

  const handleOpenChange = (open: boolean) => {
    onOpenChange?.(open)
    if (open && selectedKeys?.length) {
      // Use setTimeout to wait for the dropdown to render
      setTimeout(() => {
        const selectedElement = document.querySelector(
          `.ant-dropdown-menu-item-selected`,
        )
        selectedElement?.scrollIntoView({ block: 'center' })
      }, 0)
    }
  }

  if (!icon) return null
  if (!alwaysRender && (!items || items.length === 0)) return null

  return (
    <WaydTooltip title={tooltip}>
      <Dropdown
        menu={{
          items: menuItems,
          onClick: handleMenuClick,
          selectedKeys: selectedKeys,
          style: {
            maxHeight,
            overflowY: 'auto',
            scrollbarWidth: 'thin',
            scrollbarColor: `${token.colorTextQuaternary} ${token.colorBgElevated}`,
          },
        }}
        trigger={['click']}
        onOpenChange={handleOpenChange}
      >
        <Button type="text" shape="circle" icon={icon} />
      </Dropdown>
    </WaydTooltip>
  )
}

export default IconMenu
