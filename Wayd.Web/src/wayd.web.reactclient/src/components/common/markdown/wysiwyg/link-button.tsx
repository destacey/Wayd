'use client'

import { LinkOutlined } from '@ant-design/icons'
import type { Editor } from '@tiptap/react'
import { useEditorState } from '@tiptap/react'
import { Button, Flex, Input, Popover, Typography } from 'antd'
import { useState, type KeyboardEvent } from 'react'
import WaydTooltip from '../../wayd-tooltip'
import { toLinkHref } from './safe-urls'

interface LinkButtonProps {
  editor: Editor
  disabled?: boolean
}

/** Toolbar button that adds, changes or removes the link at the selection. */
const LinkButton = ({ editor, disabled }: LinkButtonProps) => {
  const [open, setOpen] = useState(false)
  const [input, setInput] = useState('')
  const [error, setError] = useState<string | null>(null)

  const isLink = useEditorState({
    editor,
    selector: ({ editor }) => editor.isActive('link'),
  })

  const onOpenChange = (next: boolean) => {
    if (next) {
      setInput((editor.getAttributes('link').href as string | undefined) ?? '')
      setError(null)
    }
    setOpen(next)
  }

  const apply = () => {
    const href = toLinkHref(input)
    if (!href) {
      setError(
        'Use a web address (https://…), an email link (mailto:…) or a path in Wayd (/…).',
      )
      return
    }

    const chain = editor.chain().focus()
    if (editor.state.selection.empty && !editor.isActive('link')) {
      chain
        .insertContent({
          type: 'text',
          text: href,
          marks: [{ type: 'link', attrs: { href } }],
        })
        .run()
    } else {
      chain.extendMarkRange('link').setLink({ href }).run()
    }
    setOpen(false)
  }

  const remove = () => {
    editor.chain().focus().extendMarkRange('link').unsetLink().run()
    setOpen(false)
  }

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Enter') {
      event.preventDefault()
      apply()
    }
  }

  const content = (
    <Flex vertical gap="small" style={{ width: 320 }}>
      <Input
        autoFocus
        size="small"
        aria-label="Link address"
        placeholder="https://"
        value={input}
        status={error ? 'error' : undefined}
        onChange={(event) => {
          setInput(event.target.value)
          setError(null)
        }}
        onKeyDown={onKeyDown}
      />
      {error && (
        <Typography.Text type="danger" role="alert">
          {error}
        </Typography.Text>
      )}
      <Flex justify="end" gap="small">
        {isLink && (
          <Button size="small" danger onClick={remove}>
            Remove link
          </Button>
        )}
        <Button size="small" type="primary" onClick={apply}>
          Apply
        </Button>
      </Flex>
    </Flex>
  )

  // The trigger is one stable element: a controlled Popover closes itself when its trigger
  // node is replaced between renders.
  return (
    <Popover
      trigger="click"
      open={open && !disabled}
      onOpenChange={onOpenChange}
      content={content}
      placement="bottomLeft"
      destroyOnHidden
    >
      <span>
        <WaydTooltip title={open ? undefined : 'Link'}>
          <Button
            size="small"
            color={isLink ? 'primary' : 'default'}
            variant={isLink ? 'filled' : 'text'}
            icon={<LinkOutlined />}
            aria-label="Link"
            aria-pressed={isLink}
            aria-haspopup="dialog"
            disabled={disabled}
            onMouseDown={(event) => event.preventDefault()}
          />
        </WaydTooltip>
      </span>
    </Popover>
  )
}

export default LinkButton
