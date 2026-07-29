'use client'

import dynamic from 'next/dynamic'
import '@uiw/react-md-editor/markdown-editor.css'
import '@uiw/react-markdown-preview/markdown.css'
import { MarkdownEditorFooter, useMarkdownComponentsForMDEditor } from '.'
import remarkGfm from 'remark-gfm'
import useTheme from '../../contexts/theme'
import { useEffect, useRef, useState } from 'react'
import React from 'react'

const MDEditor = dynamic(() => import('@uiw/react-md-editor'), { ssr: false })

interface MarkdownEditorProps {
  value?: string
  onChange?: (value: string) => void
  maxLength: number
}

const MarkdownEditor: React.FC<MarkdownEditorProps> = React.memo(
  ({ value, onChange, maxLength }: MarkdownEditorProps) => {
    const [isFocused, setIsFocused] = useState(false)
    const [isHovered, setIsHovered] = useState(false)
    const { token, currentMode } = useTheme()

    const editorRef = useRef<HTMLDivElement | null>(null)

    const markdownComponents = useMarkdownComponentsForMDEditor()

    const handleWrapperClick = () => {
      const textarea = editorRef.current?.querySelector('textarea')
      if (textarea) {
        textarea.focus()
      }
    }

    useEffect(() => {
      return () => {
        // Reset body scroll lock
        // Without this the user is unable to scroll the page after closing the modal
        document.body.style.overflow = ''
      }
    }, [])

    const contentLength = value?.length || 0

    return (
      <>
        <div
          data-color-mode={currentMode === 'light' ? 'light' : 'dark'}
          onMouseEnter={() => setIsHovered(true)}
          onMouseLeave={() => setIsHovered(false)}
          onClick={handleWrapperClick}
          ref={editorRef}
          style={{
            border: `1px solid ${isFocused ? token.colorPrimary : token.colorBorder}`,
            borderRadius: token.borderRadius,
            backgroundColor: token.colorBgContainer,
            boxShadow: isFocused
              ? `0 0 0 1px ${token.colorPrimaryBorder}`
              : isHovered
                ? `0 0 0 1px ${token.colorPrimaryHover}`
                : 'none',
            transition: 'box-shadow 0.3s, border-color 0.3s',
          }}
        >
          <MDEditor
            value={value ?? ''}
            onChange={(val) => val !== undefined && onChange?.(val)}
            height={300}
            preview="edit" // Options: 'live', 'edit', 'preview'
            previewOptions={{
              components: markdownComponents,
              remarkPlugins: [remarkGfm],
              style: {
                backgroundColor: token.colorBgContainer,
              },
            }}
            textareaProps={{
              maxLength: maxLength,
            }}
            onFocus={() => setIsFocused(true)}
            onBlur={() => setIsFocused(false)}
            style={{
              padding: '4px',
              fontSize: '14px',
              backgroundColor: token.colorBgContainer,
              color: token.colorText,
              outline: 'none',
              border: 'none',
              boxShadow: 'none',
            }}
          />
        </div>
        <MarkdownEditorFooter
          currentLength={contentLength}
          maxLength={maxLength}
        />
      </>
    )
  },
)

MarkdownEditor.displayName = 'MarkdownEditor'

export default MarkdownEditor
