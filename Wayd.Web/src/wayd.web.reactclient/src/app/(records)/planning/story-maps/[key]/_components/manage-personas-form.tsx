'use client'

import {
  StoryMapDetailsDto,
  StoryMapPersonaDto,
  UpdatePersonaRequest,
} from '@/src/services/wayd-api'
import {
  useAddPersonaMutation,
  useUpdatePersonaMutation,
  useDeletePersonaMutation,
  useReorderPersonaMutation,
} from '@/src/store/features/planning/story-maps-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { nextUnusedPersonaColor, personaColorPalette } from '@/src/utils'
import { DeleteOutlined, HolderOutlined, PlusOutlined } from '@ant-design/icons'
import {
  Button,
  ColorPicker,
  Flex,
  Form,
  Input,
  InputRef,
  Modal,
  Popconfirm,
  Typography,
} from 'antd'
import {
  DndContext,
  DragEndEvent,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
} from '@dnd-kit/core'
import {
  SortableContext,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import {
  CSSProperties,
  FC,
  KeyboardEvent,
  useMemo,
  useRef,
  useState,
} from 'react'
import styles from '../../_components/story-map.module.css'

const { Text } = Typography

export interface ManagePersonasFormProps {
  map: StoryMapDetailsDto
  storyMapKey: string
  onClose: () => void
}

interface PersonaCounts {
  steps: number
  tasks: number
}

type EditableTextField = 'name' | 'description'

interface PersonaRowProps {
  persona: StoryMapPersonaDto
  counts: PersonaCounts
  onUpdate: (
    persona: StoryMapPersonaDto,
    patch: Partial<UpdatePersonaRequest>,
  ) => void
  onDelete: (persona: StoryMapPersonaDto) => void
}

/** A single persona row where the color, name, and description are each click-to-edit. */
const PersonaRow: FC<PersonaRowProps> = ({
  persona,
  counts,
  onUpdate,
  onDelete,
}) => {
  const [editingField, setEditingField] = useState<EditableTextField | null>(
    null,
  )
  const [draft, setDraft] = useState('')
  const inputRef = useRef<InputRef>(null)
  const committedRef = useRef(false)

  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: persona.id })

  const sortableStyle: CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.4 : 1,
  }

  const startEdit = (field: EditableTextField, current: string) => {
    setEditingField(field)
    setDraft(current)
    committedRef.current = false
    setTimeout(() => inputRef.current?.focus(), 0)
  }

  const commit = () => {
    if (committedRef.current || editingField === null) return
    committedRef.current = true

    const field = editingField
    const value = draft.trim()
    setEditingField(null)

    if (field === 'name') {
      // Name is required — ignore an empty value and keep the current name.
      if (value && value !== persona.name) onUpdate(persona, { name: value })
    } else {
      const next = value || undefined
      if (next !== (persona.description ?? undefined)) {
        onUpdate(persona, { description: next })
      }
    }
  }

  const cancel = () => {
    committedRef.current = true
    setEditingField(null)
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      commit()
    } else if (e.key === 'Escape') {
      e.preventDefault()
      cancel()
    }
  }

  return (
    <div ref={setNodeRef} style={sortableStyle} className={styles.personaCard}>
      <Button
        type="text"
        size="small"
        icon={<HolderOutlined />}
        className={styles.personaDragHandle}
        aria-label={`Reorder ${persona.name}`}
        {...attributes}
        {...listeners}
      />

      <ColorPicker
        value={persona.color}
        size="small"
        className={styles.personaColorPicker}
        presets={[{ label: 'Personas', colors: personaColorPalette }]}
        onChangeComplete={(color) =>
          onUpdate(persona, { color: color.toHexString() })
        }
      />

      <div className={styles.personaCardBody}>
        {editingField === 'name' ? (
          <Input
            ref={inputRef}
            size="small"
            maxLength={128}
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={handleKeyDown}
            onBlur={commit}
          />
        ) : (
          <button
            type="button"
            className={styles.personaField}
            onClick={() => startEdit('name', persona.name)}
          >
            <Text strong>{persona.name}</Text>
          </button>
        )}

        {editingField === 'description' ? (
          <Input
            ref={inputRef}
            size="small"
            maxLength={256}
            placeholder="Who is this, in one line"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={handleKeyDown}
            onBlur={commit}
          />
        ) : (
          <button
            type="button"
            className={styles.personaField}
            onClick={() => startEdit('description', persona.description ?? '')}
          >
            <Text type="secondary" className={styles.personaCardDesc}>
              {persona.description || 'Who is this, in one line'}
            </Text>
          </button>
        )}

        <Text type="secondary" className={styles.personaCardCounts}>
          {counts.steps} {counts.steps === 1 ? 'step' : 'steps'}, {counts.tasks}{' '}
          {counts.tasks === 1 ? 'task' : 'tasks'}
        </Text>
      </div>

      <Popconfirm
        title="Delete this persona?"
        description="It will be untagged from every step and task."
        okText="Delete"
        okButtonProps={{ danger: true }}
        onConfirm={() => onDelete(persona)}
      >
        <Button
          type="text"
          size="small"
          danger
          icon={<DeleteOutlined />}
          aria-label={`Delete ${persona.name}`}
        />
      </Popconfirm>
    </div>
  )
}

interface AddPersonaValues {
  name: string
  description?: string
  color: string
}

const ManagePersonasForm: FC<ManagePersonasFormProps> = ({
  map,
  storyMapKey,
  onClose,
}) => {
  const messageApi = useMessage()
  const [addPersona] = useAddPersonaMutation()
  const [updatePersona] = useUpdatePersonaMutation()
  const [deletePersona] = useDeletePersonaMutation()
  const [reorderPersona] = useReorderPersonaMutation()

  const [isAdding, setIsAdding] = useState(false)
  const [form] = Form.useForm<AddPersonaValues>()

  // Require a small drag before activating, so clicking the handle's neighbours (edit fields) still
  // works and a stray click doesn't start a drag.
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
  )

  const orderedPersonas = useMemo(
    () => [...map.personas].sort((a, b) => a.order - b.order),
    [map.personas],
  )

  // The persona DTO carries no usage counts, so derive "N steps, M tasks" from the loaded graph.
  const countsByPersona = useMemo(() => {
    const counts = new Map<string, PersonaCounts>()
    for (const persona of map.personas) {
      counts.set(persona.id, { steps: 0, tasks: 0 })
    }
    for (const goal of map.goals) {
      for (const step of goal.steps) {
        for (const personaId of step.personaIds) {
          const c = counts.get(personaId)
          if (c) c.steps += 1
        }
        for (const task of step.tasks) {
          for (const personaId of task.personaIds) {
            const c = counts.get(personaId)
            if (c) c.tasks += 1
          }
        }
      }
    }
    return counts
  }, [map])

  // Update a single field, sending the full request (the patched field plus the current values).
  const handleUpdate = async (
    persona: StoryMapPersonaDto,
    patch: Partial<UpdatePersonaRequest>,
  ) => {
    try {
      await updatePersona({
        storyMapId: map.id,
        storyMapKey,
        personaId: persona.id,
        request: {
          name: persona.name,
          description: persona.description,
          color: persona.color,
          ...patch,
        },
      }).unwrap()
    } catch {
      messageApi.error('Failed to update persona.')
    }
  }

  const handleDelete = async (persona: StoryMapPersonaDto) => {
    try {
      await deletePersona({
        storyMapId: map.id,
        storyMapKey,
        personaId: persona.id,
      }).unwrap()
    } catch {
      messageApi.error('Failed to delete persona.')
    }
  }

  const handleDragEnd = async (event: DragEndEvent) => {
    const { active, over } = event
    if (!over || active.id === over.id) return

    const oldIndex = orderedPersonas.findIndex((p) => p.id === active.id)
    const newIndex = orderedPersonas.findIndex((p) => p.id === over.id)
    if (oldIndex === -1 || newIndex === -1) return

    try {
      await reorderPersona({
        storyMapId: map.id,
        storyMapKey,
        personaId: String(active.id),
        newOrder: newIndex,
      }).unwrap()
    } catch {
      messageApi.error('Failed to reorder persona.')
    }
  }

  const startAdd = () => {
    setIsAdding(true)
    form.setFieldsValue({
      name: '',
      description: undefined,
      color: nextUnusedPersonaColor(map.personas.map((p) => p.color)),
    })
  }

  const cancelAdd = () => {
    setIsAdding(false)
    form.resetFields()
  }

  const submitAdd = async () => {
    let values: AddPersonaValues
    try {
      values = await form.validateFields()
    } catch {
      return
    }

    try {
      await addPersona({
        storyMapId: map.id,
        storyMapKey,
        request: {
          name: values.name.trim(),
          description: values.description?.trim() || undefined,
          color: values.color,
        },
      }).unwrap()
      cancelAdd()
    } catch {
      messageApi.error('Failed to add persona.')
    }
  }

  return (
    <Modal
      title="Personas"
      open
      onCancel={onClose}
      footer={null}
      destroyOnHidden
    >
      <Text type="secondary" className={styles.personaModalSubtitle}>
        Scoped to this map. Tag them on steps and tasks to filter.
      </Text>

      <div className={styles.personaList}>
        <DndContext
          sensors={sensors}
          collisionDetection={closestCenter}
          onDragEnd={handleDragEnd}
        >
          <SortableContext
            items={orderedPersonas.map((p) => p.id)}
            strategy={verticalListSortingStrategy}
          >
            {orderedPersonas.map((persona) => (
              <PersonaRow
                key={persona.id}
                persona={persona}
                counts={
                  countsByPersona.get(persona.id) ?? { steps: 0, tasks: 0 }
                }
                onUpdate={handleUpdate}
                onDelete={handleDelete}
              />
            ))}
          </SortableContext>
        </DndContext>

        {isAdding && (
          <div className={styles.personaCard}>
            <Form
              form={form}
              size="small"
              className={styles.personaEditor}
              onFinish={submitAdd}
            >
              <Flex gap={8} align="center" className={styles.personaEditorRow}>
                <Form.Item
                  name="color"
                  noStyle
                  getValueFromEvent={(color) => color.toHexString()}
                >
                  <ColorPicker
                    size="small"
                    className={styles.personaColorPicker}
                    presets={[
                      { label: 'Personas', colors: personaColorPalette },
                    ]}
                  />
                </Form.Item>
                <Form.Item
                  name="name"
                  className={styles.personaEditorField}
                  rules={[{ required: true, message: 'Name is required.' }]}
                >
                  <Input placeholder="Persona name" maxLength={128} autoFocus />
                </Form.Item>
              </Flex>
              <Form.Item
                name="description"
                className={styles.personaEditorField}
              >
                <Input placeholder="Who is this, in one line" maxLength={256} />
              </Form.Item>
              <Flex gap={8} justify="flex-end">
                <Button size="small" onClick={cancelAdd}>
                  Cancel
                </Button>
                <Button size="small" type="primary" onClick={submitAdd}>
                  Add
                </Button>
              </Flex>
            </Form>
          </div>
        )}
      </div>

      {!isAdding && (
        <Button
          type="dashed"
          block
          icon={<PlusOutlined />}
          className={styles.personaAddButton}
          onClick={startAdd}
        >
          Add persona
        </Button>
      )}
    </Modal>
  )
}

export default ManagePersonasForm
