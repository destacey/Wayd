'use client'

import { useEffect } from 'react'

export const useDocumentTitle = (
  title: string | (() => string),
  defaultTitle: string = '',
): void => {
  useEffect(() => {
    const previousTitle = document.title
    const newTitle = typeof title === 'function' ? title() : title
    document.title = newTitle

    return () => {
      document.title = defaultTitle || previousTitle
    }
  }, [title, defaultTitle])
}
