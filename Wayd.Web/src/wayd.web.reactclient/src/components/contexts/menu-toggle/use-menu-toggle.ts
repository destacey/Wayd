'use client'

import { useContext } from 'react'
import { MenuToggleContextType } from './types'
import { MenuToggleContext } from './menu-toggle-context'

const useMenuToggle = (): MenuToggleContextType => {
  const context = useContext(MenuToggleContext)
  if (!context) {
    throw new Error('useMenuToggle must be used within an MenuToggleProvider')
  }
  return context
}

export default useMenuToggle
