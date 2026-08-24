'use client'

import { Tag, Input, InputRef } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import { useState, useRef } from 'react'

interface TagInputProps {
  value?: string[]
  onChange?: (value: string[]) => void
  placeholder?: string
}

const TagInput = ({ value = [], onChange, placeholder }: TagInputProps) => {
  const [inputVisible, setInputVisible] = useState(false)
  const [inputValue, setInputValue] = useState('')
  const inputRef = useRef<InputRef>(null)

  const handleClose = (removed: string) => {
    onChange?.(value.filter((tag) => tag !== removed))
  }

  const showInput = () => {
    setInputVisible(true)
    setTimeout(() => inputRef.current?.focus(), 0)
  }

  const handleInputConfirm = () => {
    const trimmed = inputValue.trim()
    if (trimmed && !value.includes(trimmed)) {
      onChange?.([...value, trimmed])
    }
    setInputVisible(false)
    setInputValue('')
  }

  return (
    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
      {value.map((tag) => (
        <Tag key={tag} closable onClose={() => handleClose(tag)}>
          {tag}
        </Tag>
      ))}
      {inputVisible ? (
        <Input
          ref={inputRef}
          size="small"
          style={{ width: 200 }}
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
          onBlur={handleInputConfirm}
          onPressEnter={handleInputConfirm}
          placeholder={placeholder}
        />
      ) : (
        <Tag
          onClick={showInput}
          onKeyDown={(e) => (e.key === 'Enter' || e.key === ' ') && showInput()}
          role="button"
          tabIndex={0}
          aria-label={placeholder ?? 'Add'}
          style={{ cursor: 'pointer', borderStyle: 'dashed' }}
          icon={<PlusOutlined />}
        >
          {placeholder ?? 'Add'}
        </Tag>
      )}
    </div>
  )
}

export default TagInput
