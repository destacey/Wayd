'use client'

import {
  StoryMapDetailsDto,
  StoryMapPersonaDto,
  StoryMapTaskDto,
} from '@/src/services/wayd-api'
import { LabeledContent } from '@/src/components/common/content'
import { useMessage } from '@/src/components/contexts/messaging'
import { getDrawerWidthPixels } from '@/src/utils'
import {
  useAddChecklistItemMutation,
  useLinkWorkItemMutation,
  useRemoveChecklistItemMutation,
  useRenameChecklistItemMutation,
  useSetChecklistItemCheckedMutation,
  useUnlinkWorkItemMutation,
} from '@/src/store/features/planning/story-maps-api'
import {
  CloseOutlined,
  DeleteOutlined,
  DisconnectOutlined,
  LinkOutlined,
  PlusOutlined,
} from '@ant-design/icons'
import {
  Button,
  Checkbox,
  Drawer,
  Flex,
  Input,
  InputNumber,
  Popconfirm,
  Select,
  Typography,
} from 'antd'
import { FC, KeyboardEvent, useState } from 'react'
import styles from '../../_components/story-map.module.css'

const { TextArea } = Input
const { Text } = Typography

export interface TaskDrawerProps {
  /** The whole map, so the drawer can offer every swim lane and persona. */
  map: StoryMapDetailsDto
  /** Cache key for optimistic updates. */
  storyMapKey: string
  /** Re-resolved from the RTK cache each render by the page, so edits flow through. Null closes. */
  task: StoryMapTaskDto | null
  canUpdate: boolean
  onClose: () => void
  onRenameTask: (taskId: string, title: string) => void
  /** Undefined clears the description. */
  onSetTaskDescription: (
    taskId: string,
    description: string | undefined,
  ) => void
  onDeleteTask: (taskId: string) => void
  onToggleTaskPersona: (taskId: string, personaId: string) => void
  /** Moves the task to another swim lane within its current step. */
  onMoveTaskToLane: (task: StoryMapTaskDto, swimLaneId: string) => void
}

/**
 * Details for one task. On a wide viewport this is only the narrow-screen fallback — the board
 * renders {@link TaskPanel} inline as a Layout sider instead, so the board reflows beside it rather
 * than being covered. Non-modal either way, so the board stays live and re-points as cards are
 * clicked.
 */
const TaskDrawer: FC<TaskDrawerProps> = (props) => {
  const { task, onClose } = props

  return (
    <Drawer
      mask={false}
      title={task ? 'Task Details' : ''}
      placement="right"
      onClose={onClose}
      open={task !== null}
      size={getDrawerWidthPixels()}
      // Discards the body's per-task drafts, so reopening never shows stale half-typed input.
      destroyOnHidden
    >
      {/* Keyed so switching tasks while open remounts the body, for the same reason. */}
      {task && <TaskDrawerBody key={task.id} {...props} task={task} />}
    </Drawer>
  )
}

/** Width of the sider, and the gutter the board leaves for it. */
export const TASK_PANEL_WIDTH = 360

/**
 * The same details as a Layout sider, for viewports wide enough to give up the space. Takes its
 * width from the flex row, so the board's grid re-divides what is left instead of scrolling under
 * an overlay.
 */
export const TaskPanel: FC<TaskDrawerProps> = (props) => {
  const { task, onClose } = props
  if (!task) return null

  return (
    <div className={styles.taskPanel} style={{ width: TASK_PANEL_WIDTH }}>
      <div className={styles.taskPanelHeader}>
        <span className={styles.taskPanelTitle}>Task Details</span>
        <Button
          type="text"
          size="small"
          icon={<CloseOutlined />}
          aria-label="Close"
          onClick={onClose}
        />
      </div>
      <div className={styles.taskPanelBody}>
        <TaskDrawerBody key={task.id} {...props} task={task} />
      </div>
    </div>
  )
}

type TaskDrawerBodyProps = Omit<TaskDrawerProps, 'task'> & {
  task: StoryMapTaskDto
}

const TaskDrawerBody: FC<TaskDrawerBodyProps> = ({
  map,
  storyMapKey,
  task,
  canUpdate,
  onClose,
  onRenameTask,
  onSetTaskDescription,
  onDeleteTask,
  onToggleTaskPersona,
  onMoveTaskToLane,
}) => {
  const messageApi = useMessage()

  const [addChecklistItem] = useAddChecklistItemMutation()
  const [renameChecklistItem] = useRenameChecklistItemMutation()
  const [setChecklistItemChecked] = useSetChecklistItemCheckedMutation()
  const [removeChecklistItem] = useRemoveChecklistItemMutation()
  const [linkWorkItem] = useLinkWorkItemMutation()
  const [unlinkWorkItem] = useUnlinkWorkItemMutation()

  // Drafts saved on blur, so typing does not fire a request per keystroke.
  const [title, setTitle] = useState(task.title)
  const [description, setDescription] = useState(task.description)

  const [isAddingItem, setIsAddingItem] = useState(false)
  const [newItemName, setNewItemName] = useState('')

  const [isLinking, setIsLinking] = useState(false)
  const [workItemId, setWorkItemId] = useState<number | null>(null)

  const common = { storyMapId: map.id, storyMapKey, taskId: task.id }

  // Title is required, so an emptied field reverts rather than saving a blank.
  const handleSaveTitle = () => {
    const next = title.trim()
    if (!next) {
      setTitle(task.title)
      return
    }
    if (next === task.title) return
    onRenameTask(task.id, next)
  }

  const handleTitleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      e.currentTarget.blur()
    } else if (e.key === 'Escape') {
      e.preventDefault()
      // Without this the Drawer's own handler also fires and closes it mid-edit.
      e.stopPropagation()
      setTitle(task.title)
    }
  }

  const handleDescriptionKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    // Enter inserts a newline here, unlike the single-line title.
    if (e.key === 'Escape') {
      e.preventDefault()
      e.stopPropagation()
      setDescription(task.description)
    }
  }

  // undefined, not '': JSON.stringify omits the property, so the API nulls the column.
  const handleSaveDescription = () => {
    const next = description?.trim()
    if ((next ?? '') === (task.description?.trim() ?? '')) return
    onSetTaskDescription(task.id, next || undefined)
  }

  const handleAddItem = () => {
    const name = newItemName.trim()
    if (!name) {
      setIsAddingItem(false)
      return
    }

    // Stays open so several items can be added in succession.
    setNewItemName('')
    addChecklistItem({ ...common, request: { name } })
      .unwrap()
      .catch(() => messageApi.error('Failed to add checklist item.'))
  }

  const handleItemKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      handleAddItem()
    } else if (e.key === 'Escape') {
      e.preventDefault()
      e.stopPropagation()
      setIsAddingItem(false)
      setNewItemName('')
    }
  }

  const handleLink = () => {
    if (workItemId === null) {
      setIsLinking(false)
      return
    }

    setIsLinking(false)
    linkWorkItem({ ...common, request: { workItemId } })
      .unwrap()
      .catch(() => messageApi.error('Failed to link work item.'))
  }

  const orderedChecklist = [...task.checklist].sort((a, b) => a.order - b.order)
  const orderedPersonas = [...map.personas].sort((a, b) => a.order - b.order)
  const orderedLanes = [...map.swimLanes].sort((a, b) => a.order - b.order)

  return (
    <Flex vertical gap="middle">
      {/* Not the board's InlineEditText: it styles itself with the board's --sm-* variables, which
          are set on the page container and so do not resolve in the drawer's portal. */}
      <LabeledContent label="Title">
        {/* Auto-sizing so a 128-character title wraps instead of scrolling out of view. */}
        <div className={styles.countOnFocus}>
          <TextArea
            value={title}
            disabled={!canUpdate}
            maxLength={128}
            showCount
            autoSize={{ minRows: 1 }}
            aria-label="Task title"
            onChange={(e) => setTitle(e.target.value)}
            onBlur={handleSaveTitle}
            onKeyDown={handleTitleKeyDown}
          />
        </div>
      </LabeledContent>

      {/* ── Linked work item ── */}
      {task.linkedWorkItemId !== undefined ? (
        <Flex align="center" justify="space-between" gap={8}>
          <span className={styles.drawerLinkedItem}>
            <LinkOutlined /> Work item {task.linkedWorkItemId}
          </span>
          {canUpdate && (
            <Button
              type="text"
              size="small"
              icon={<DisconnectOutlined />}
              aria-label="Unlink work item"
              onClick={() =>
                unlinkWorkItem(common)
                  .unwrap()
                  .catch(() => messageApi.error('Failed to unlink work item.'))
              }
            />
          )}
        </Flex>
      ) : (
        canUpdate &&
        (isLinking ? (
          <Flex gap={8}>
            <InputNumber
              autoFocus
              min={1}
              placeholder="Work item ID"
              style={{ flex: 1 }}
              value={workItemId}
              onChange={setWorkItemId}
              onPressEnter={handleLink}
            />
            <Button onClick={handleLink}>Link</Button>
          </Flex>
        ) : (
          <Button
            block
            icon={<LinkOutlined />}
            onClick={() => setIsLinking(true)}
          >
            Link work item
          </Button>
        ))
      )}

      {/* ── Swim lane ── */}
      <LabeledContent label="Swim Lane">
        <Select
          value={task.swimLaneId}
          disabled={!canUpdate}
          style={{ width: '100%' }}
          onChange={(swimLaneId) => onMoveTaskToLane(task, swimLaneId)}
          options={orderedLanes.map((lane) => ({
            value: lane.id,
            label: lane.name,
          }))}
        />
      </LabeledContent>

      {/* ── Description ── */}
      {/* maxLength mirrors SetTaskDescriptionCommandValidator. */}
      <LabeledContent label="Description">
        <div className={styles.countOnFocus}>
          <TextArea
            value={description}
            disabled={!canUpdate}
            placeholder="What still needs deciding?"
            maxLength={2048}
            showCount
            autoSize={{ minRows: 3 }}
            aria-label="Task description"
            onChange={(e) => setDescription(e.target.value)}
            onBlur={handleSaveDescription}
            onKeyDown={handleDescriptionKeyDown}
          />
        </div>
      </LabeledContent>

      {/* ── Personas ── */}
      {orderedPersonas.length > 0 && (
        <LabeledContent label="Personas">
          <Flex gap={8} wrap>
            {orderedPersonas.map((persona) => (
              <PersonaChip
                key={persona.id}
                persona={persona}
                isLinked={task.personaIds.includes(persona.id)}
                disabled={!canUpdate}
                onToggle={() => onToggleTaskPersona(task.id, persona.id)}
              />
            ))}
          </Flex>
        </LabeledContent>
      )}

      {/* ── Checklist ── */}
      <LabeledContent label="Checklist">
        {/* LabeledContent lays its children out inline, so the rows need their own column. */}
        <Flex vertical gap={6} style={{ width: '100%' }}>
          {orderedChecklist.map((item) => (
            // Centred so the checkbox and delete stay aligned when the name swaps to its editor.
            //
            // Escape is stopped here: Typography's editor cancels on it, but the event would carry
            // on to the Drawer's own handler and close the whole drawer mid-edit.
            <Flex
              key={item.id}
              align="center"
              gap={8}
              onKeyDown={(e) => {
                if (e.key === 'Escape') e.stopPropagation()
              }}
            >
              <Checkbox
                checked={item.isChecked}
                disabled={!canUpdate}
                aria-label={item.name}
                onChange={(e) =>
                  setChecklistItemChecked({
                    ...common,
                    itemId: item.id,
                    request: { isChecked: e.target.checked },
                  })
                    .unwrap()
                    .catch(() =>
                      messageApi.error('Failed to update checklist item.'),
                    )
                }
              />
              <div className={styles.drawerChecklistName}>
                <Text
                  delete={item.isChecked}
                  type={item.isChecked ? 'secondary' : undefined}
                  editable={
                    canUpdate
                      ? {
                          // Omitting 'icon' from triggerType hides the pencil; enterIcon null drops
                          // the corner glyph, matching the plain textareas above.
                          triggerType: ['text'],
                          enterIcon: null,
                          maxLength: 256,
                          autoSize: { minRows: 1 },
                          tooltip: 'Click to rename',
                          onChange: (name) => {
                            const next = name.trim()
                            if (!next || next === item.name) return
                            renameChecklistItem({
                              ...common,
                              itemId: item.id,
                              request: { name: next },
                            })
                              .unwrap()
                              .catch(() =>
                                messageApi.error(
                                  'Failed to rename checklist item.',
                                ),
                              )
                          },
                        }
                      : false
                  }
                >
                  {item.name}
                </Text>
              </div>
              {canUpdate && (
                <Button
                  type="text"
                  danger
                  size="small"
                  icon={<DeleteOutlined />}
                  aria-label="Delete checklist item"
                  onClick={() =>
                    removeChecklistItem({ ...common, itemId: item.id })
                      .unwrap()
                      .catch(() =>
                        messageApi.error('Failed to delete checklist item.'),
                      )
                  }
                />
              )}
            </Flex>
          ))}

          {canUpdate &&
            (isAddingItem ? (
              <Input
                autoFocus
                size="small"
                placeholder="Checklist item"
                maxLength={256}
                value={newItemName}
                onChange={(e) => setNewItemName(e.target.value)}
                onKeyDown={handleItemKeyDown}
                onBlur={handleAddItem}
              />
            ) : (
              <Button
                type="text"
                size="small"
                icon={<PlusOutlined />}
                className={styles.drawerAddItem}
                onClick={() => setIsAddingItem(true)}
              >
                Add item
              </Button>
            ))}
        </Flex>
      </LabeledContent>

      {/* ── Delete ── */}
      {canUpdate && (
        <Popconfirm
          title="Delete this task?"
          okText="Delete"
          okButtonProps={{ danger: true }}
          onConfirm={() => {
            onDeleteTask(task.id)
            onClose()
          }}
        >
          <Button
            type="text"
            danger
            size="small"
            icon={<DeleteOutlined />}
            className={styles.drawerDelete}
          >
            Delete task
          </Button>
        </Popconfirm>
      )}
    </Flex>
  )
}

/**
 * A persona as a labelled chip — the filter bar's shape, toggling the link instead of filtering.
 * Linked chips take the persona's own colour rather than the theme primary, so several tagged
 * personas stay tellable apart.
 */
const PersonaChip: FC<{
  persona: StoryMapPersonaDto
  isLinked: boolean
  disabled: boolean
  onToggle: () => void
}> = ({ persona, isLinked, disabled, onToggle }) => (
  <Button
    size="small"
    variant="outlined"
    color="default"
    disabled={disabled}
    aria-pressed={isLinked}
    // Unset when unlinked so the theme's own border and text tokens apply.
    style={
      isLinked
        ? { borderColor: persona.color, color: persona.color }
        : undefined
    }
    icon={
      <span
        className={`${styles.personaDot} ${
          isLinked ? '' : styles.personaDotUnlinked
        }`}
        style={isLinked ? { backgroundColor: persona.color } : undefined}
      />
    }
    onClick={onToggle}
  >
    {persona.name}
  </Button>
)

export default TaskDrawer
