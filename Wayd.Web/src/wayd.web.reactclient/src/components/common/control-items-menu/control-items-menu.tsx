import { ControlOutlined } from '@ant-design/icons'
import { Button, Dropdown, MenuProps } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { useEffect, useRef, useState } from 'react'

export interface ControlItemsMenuProps {
  items: MenuProps['items'] | ItemType[]
}

const ControlItemsMenu = ({ items }: ControlItemsMenuProps) => {
  const [open, setOpen] = useState(false)
  const triggerRef = useRef<HTMLButtonElement | null>(null)

  useEffect(() => {
    if (!open) return

    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as HTMLElement | null
      if (!target) return

      const clickedTrigger = !!triggerRef.current?.contains(target)
      const clickedMenu = !!target.closest('.wayd-control-items-menu-overlay')

      if (!clickedTrigger && !clickedMenu) {
        setOpen(false)
      }
    }

    document.addEventListener('pointerdown', onPointerDown, true)
    return () => document.removeEventListener('pointerdown', onPointerDown, true)
  }, [open])

  if (!items || items.length === 0) return null
  return (
    <Dropdown
      menu={{ items: items }}
      trigger={['click']}
      open={open}
      onOpenChange={(nextOpen) => setOpen(nextOpen)}
      classNames={{ root: 'wayd-control-items-menu-overlay' }}
    >
      <Button
        ref={triggerRef}
        type="text"
        shape="circle"
        icon={<ControlOutlined />}
      />
    </Dropdown>
  )
}

export default ControlItemsMenu
