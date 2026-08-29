'use client'

import { useContext } from 'react'
import { ThemeContext } from './theme-context'
import { ThemeContextType } from './types'

const useTheme = (): ThemeContextType => {
  const context = useContext(ThemeContext)
  if (!context) {
    throw new Error('useTheme must be used within an ThemeProvider')
  }
  return context
}

export default useTheme
