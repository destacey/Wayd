'use client'

import Icon, {
  BoldOutlined,
  CheckSquareOutlined,
  CodeOutlined,
  FontSizeOutlined,
  ItalicOutlined,
  LineOutlined,
  OrderedListOutlined,
  RedoOutlined,
  StrikethroughOutlined,
  TableOutlined,
  UnderlineOutlined,
  UndoOutlined,
  UnorderedListOutlined,
} from '@ant-design/icons'
import type { Editor } from '@tiptap/react'
import { useEditorState } from '@tiptap/react'
import { Button, Divider, Dropdown, type MenuProps } from 'antd'
import type { MouseEvent, ReactNode } from 'react'
import WaydTooltip from '../../wayd-tooltip'
import LinkButton from './link-button'
import styles from './wysiwyg-markdown-editor.module.css'

const QuoteSvg = () => (
  <svg
    viewBox="0 0 24 24"
    width="1em"
    height="1em"
    fill="currentColor"
    aria-hidden
  >
    <path d="M7.2 6C4.9 7.2 3.5 9.5 3.5 12.3V18h6.3v-6H6.6c0-1.6.9-3 2.3-3.8L7.2 6zm10 0c-2.3 1.2-3.7 3.5-3.7 6.3V18h6.3v-6h-3.2c0-1.6.9-3 2.3-3.8L17.2 6z" />
  </svg>
)

const CodeBlockSvg = () => (
  <svg
    viewBox="0 0 24 24"
    width="1em"
    height="1em"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.8"
    aria-hidden
  >
    <rect x="3" y="4" width="18" height="16" rx="2" />
    <path
      d="M9.5 9.5 7 12l2.5 2.5M14.5 9.5 17 12l-2.5 2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
    />
  </svg>
)

const isMac = () =>
  typeof navigator !== 'undefined' &&
  /Mac|iPhone|iPad/.test(navigator.userAgent)

/** Keeps the editor's selection: a toolbar click must not move focus out of the text first. */
const keepSelection = (event: MouseEvent) => event.preventDefault()

interface ToolbarButtonProps {
  label: string
  /** Keys after the modifier, e.g. `B` for Ctrl+B / ⌘B. */
  shortcut?: string
  icon: ReactNode
  /** Set for a toggle; reported as `aria-pressed`. */
  pressed?: boolean
  disabled?: boolean
  onClick: () => void
}

const ToolbarButton = ({
  label,
  shortcut,
  icon,
  pressed,
  disabled,
  onClick,
}: ToolbarButtonProps) => {
  const keys = shortcut ? `${isMac() ? '⌘' : 'Ctrl+'}${shortcut}` : undefined
  return (
    <WaydTooltip title={keys ? `${label} (${keys})` : label}>
      <Button
        size="small"
        color={pressed ? 'primary' : 'default'}
        variant={pressed ? 'filled' : 'text'}
        icon={icon}
        aria-label={label}
        aria-pressed={pressed}
        aria-keyshortcuts={
          shortcut ? `${isMac() ? 'Meta' : 'Control'}+${shortcut}` : undefined
        }
        disabled={disabled}
        onMouseDown={keepSelection}
        onClick={onClick}
      />
    </WaydTooltip>
  )
}

const headingItems = [
  { key: '0', label: 'Normal text' },
  { key: '1', label: 'Heading 1' },
  { key: '2', label: 'Heading 2' },
  { key: '3', label: 'Heading 3' },
]

interface MarkdownToolbarProps {
  editor: Editor
  /** The editable element's id, for `aria-controls`. */
  editorId?: string
}

/** Formatting controls for `WysiwygMarkdownEditor`, built from antd buttons and theme tokens only. */
const MarkdownToolbar = ({ editor, editorId }: MarkdownToolbarProps) => {
  const state = useEditorState({
    editor,
    selector: ({ editor }) => ({
      bold: editor.isActive('bold'),
      italic: editor.isActive('italic'),
      underline: editor.isActive('underline'),
      strike: editor.isActive('strike'),
      code: editor.isActive('code'),
      heading:
        [1, 2, 3].find((level) => editor.isActive('heading', { level })) ?? 0,
      bulletList: editor.isActive('bulletList'),
      orderedList: editor.isActive('orderedList'),
      taskList: editor.isActive('taskList'),
      blockquote: editor.isActive('blockquote'),
      codeBlock: editor.isActive('codeBlock'),
      inTable: editor.isActive('table'),
      canUndo: editor.can().undo(),
      canRedo: editor.can().redo(),
    }),
  })

  const run = () => editor.chain().focus()

  const onHeading: MenuProps['onClick'] = ({ key }) => {
    const level = Number(key)
    if (level === 0) run().setParagraph().run()
    else
      run()
        .setHeading({ level: level as 1 | 2 | 3 })
        .run()
  }

  const tableItems: MenuProps['items'] = [
    { key: 'insert', label: 'Insert table', disabled: state.inTable },
    { type: 'divider' },
    { key: 'rowBefore', label: 'Add row above', disabled: !state.inTable },
    { key: 'rowAfter', label: 'Add row below', disabled: !state.inTable },
    {
      key: 'columnBefore',
      label: 'Add column before',
      disabled: !state.inTable,
    },
    { key: 'columnAfter', label: 'Add column after', disabled: !state.inTable },
    { type: 'divider' },
    { key: 'deleteRow', label: 'Delete row', disabled: !state.inTable },
    { key: 'deleteColumn', label: 'Delete column', disabled: !state.inTable },
    {
      key: 'deleteTable',
      label: 'Delete table',
      danger: true,
      disabled: !state.inTable,
    },
  ]

  const onTable: MenuProps['onClick'] = ({ key }) => {
    const chain = run()
    const commands: Record<string, () => boolean> = {
      insert: () =>
        chain.insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run(),
      rowBefore: () => chain.addRowBefore().run(),
      rowAfter: () => chain.addRowAfter().run(),
      columnBefore: () => chain.addColumnBefore().run(),
      columnAfter: () => chain.addColumnAfter().run(),
      deleteRow: () => chain.deleteRow().run(),
      deleteColumn: () => chain.deleteColumn().run(),
      deleteTable: () => chain.deleteTable().run(),
    }
    commands[key]?.()
  }

  return (
    <div
      className={styles.toolbar}
      role="toolbar"
      aria-label="Formatting"
      aria-controls={editorId}
    >
      <WaydTooltip title="Text style">
        <Dropdown
          trigger={['click']}
          menu={{
            items: headingItems,
            selectable: true,
            selectedKeys: [String(state.heading)],
            onClick: onHeading,
          }}
        >
          <Button
            size="small"
            variant="text"
            color="default"
            icon={<FontSizeOutlined />}
            aria-label="Text style"
            aria-haspopup="menu"
            onMouseDown={keepSelection}
          >
            {headingItems[state.heading].label}
          </Button>
        </Dropdown>
      </WaydTooltip>

      <Divider orientation="vertical" className={styles.divider} />
      <ToolbarButton
        label="Bold"
        shortcut="B"
        icon={<BoldOutlined />}
        pressed={state.bold}
        onClick={() => run().toggleBold().run()}
      />
      <ToolbarButton
        label="Italic"
        shortcut="I"
        icon={<ItalicOutlined />}
        pressed={state.italic}
        onClick={() => run().toggleItalic().run()}
      />
      <ToolbarButton
        label="Underline"
        shortcut="U"
        icon={<UnderlineOutlined />}
        pressed={state.underline}
        onClick={() => run().toggleUnderline().run()}
      />
      <ToolbarButton
        label="Strikethrough"
        shortcut="Shift+S"
        icon={<StrikethroughOutlined />}
        pressed={state.strike}
        onClick={() => run().toggleStrike().run()}
      />
      <ToolbarButton
        label="Inline code"
        shortcut="E"
        icon={<CodeOutlined />}
        pressed={state.code}
        onClick={() => run().toggleCode().run()}
      />
      <LinkButton editor={editor} />

      <Divider orientation="vertical" className={styles.divider} />
      <ToolbarButton
        label="Bulleted list"
        shortcut="Shift+8"
        icon={<UnorderedListOutlined />}
        pressed={state.bulletList}
        onClick={() => run().toggleBulletList().run()}
      />
      <ToolbarButton
        label="Numbered list"
        shortcut="Shift+7"
        icon={<OrderedListOutlined />}
        pressed={state.orderedList}
        onClick={() => run().toggleOrderedList().run()}
      />
      <ToolbarButton
        label="Task list"
        shortcut="Shift+9"
        icon={<CheckSquareOutlined />}
        pressed={state.taskList}
        onClick={() => run().toggleTaskList().run()}
      />
      <ToolbarButton
        label="Quote"
        shortcut="Shift+B"
        icon={<Icon component={QuoteSvg} />}
        pressed={state.blockquote}
        onClick={() => run().toggleBlockquote().run()}
      />
      <ToolbarButton
        label="Code block"
        shortcut="Alt+C"
        icon={<Icon component={CodeBlockSvg} />}
        pressed={state.codeBlock}
        onClick={() => run().toggleCodeBlock().run()}
      />

      <Divider orientation="vertical" className={styles.divider} />
      <WaydTooltip title="Table">
        <Dropdown
          trigger={['click']}
          menu={{ items: tableItems, onClick: onTable }}
        >
          <Button
            size="small"
            color={state.inTable ? 'primary' : 'default'}
            variant={state.inTable ? 'filled' : 'text'}
            icon={<TableOutlined />}
            aria-label="Table"
            aria-haspopup="menu"
            onMouseDown={keepSelection}
          />
        </Dropdown>
      </WaydTooltip>
      <ToolbarButton
        label="Horizontal rule"
        icon={<LineOutlined />}
        onClick={() => run().setHorizontalRule().run()}
      />

      <Divider orientation="vertical" className={styles.divider} />
      <ToolbarButton
        label="Undo"
        shortcut="Z"
        icon={<UndoOutlined />}
        disabled={!state.canUndo}
        onClick={() => run().undo().run()}
      />
      <ToolbarButton
        label="Redo"
        shortcut="Shift+Z"
        icon={<RedoOutlined />}
        disabled={!state.canRedo}
        onClick={() => run().redo().run()}
      />
    </div>
  )
}

export default MarkdownToolbar
