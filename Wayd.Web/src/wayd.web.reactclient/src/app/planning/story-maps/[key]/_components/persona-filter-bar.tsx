'use client'

import { StoryMapDetailsDto, StoryMapPersonaDto } from '@/src/services/wayd-api'
import { useAddPersonaMutation } from '@/src/store/features/planning/story-maps-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { nextUnusedPersonaColor } from '@/src/utils'
import { PlusOutlined, SettingOutlined, TeamOutlined } from '@ant-design/icons'
import { Button, Flex, Input, InputRef } from 'antd'
import { FC, KeyboardEvent, useMemo, useRef, useState } from 'react'
import { countBoard } from './board-counts'
import styles from '../../_components/story-map.module.css'
import { WaydTooltip } from '@/src/components/common'

export interface PersonaFilterBarProps {
  storyMapId: string
  /** The route key used as the getStoryMap cache key (for optimistic updates). */
  storyMapKey: string
  /** Source of the goal/step/task totals shown on the right. */
  map: StoryMapDetailsDto
  personas: StoryMapPersonaDto[]
  /** The currently selected persona id, or null for "All". */
  selectedPersonaId: string | null
  onSelectPersona: (personaId: string | null) => void
  canUpdate: boolean
  onManage: () => void
}

const ColorDot: FC<{ color: string }> = ({ color }) => (
  <span className={styles.personaDot} style={{ backgroundColor: color }} />
)

const CountItem: FC<{ value: number; singular: string }> = ({
  value,
  singular,
}) => (
  <span>
    <span className={styles.countValue}>{value}</span>{' '}
    {value === 1 ? singular : `${singular}s`}
  </span>
)

const PersonaFilterBar: FC<PersonaFilterBarProps> = ({
  storyMapId,
  storyMapKey,
  map,
  personas,
  selectedPersonaId,
  onSelectPersona,
  canUpdate,
  onManage,
}) => {
  const messageApi = useMessage()
  const [addPersona] = useAddPersonaMutation()

  const counts = useMemo(
    () => countBoard(map, selectedPersonaId),
    [map, selectedPersonaId],
  )

  const [isQuickAdding, setIsQuickAdding] = useState(false)
  const [quickAddName, setQuickAddName] = useState('')
  const inputRef = useRef<InputRef>(null)
  // Enter closes the input, which fires onBlur — this guard stops that blur from committing a second
  // time in the same open session.
  const committedRef = useRef(false)

  const startQuickAdd = () => {
    setQuickAddName('')
    committedRef.current = false
    setIsQuickAdding(true)
    // Focus the input on the next tick, after it renders.
    setTimeout(() => inputRef.current?.focus(), 0)
  }

  const cancelQuickAdd = () => {
    setIsQuickAdding(false)
    setQuickAddName('')
  }

  const commitQuickAdd = () => {
    if (committedRef.current) return
    committedRef.current = true

    const name = quickAddName.trim()
    if (!name) {
      cancelQuickAdd()
      return
    }

    // Close the input immediately — the optimistic cache patch renders the chip right away, so we
    // don't block on the request. Errors roll the chip back (in the mutation) and surface a toast.
    const color = nextUnusedPersonaColor(personas.map((p) => p.color))
    cancelQuickAdd()

    addPersona({ storyMapId, storyMapKey, request: { name, color } })
      .unwrap()
      .catch(() => messageApi.error('Failed to add persona.'))
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      commitQuickAdd()
    } else if (e.key === 'Escape') {
      e.preventDefault()
      cancelQuickAdd()
    }
  }

  return (
    <Flex align="center" gap={8} wrap data-tour="persona-bar">
      <TeamOutlined className={styles.personaBarIcon} aria-hidden />

      <Button
        size="small"
        variant="outlined"
        color={selectedPersonaId === null ? 'primary' : 'default'}
        onClick={() => onSelectPersona(null)}
      >
        All
      </Button>

      {personas.map((persona) => {
        const isSelected = selectedPersonaId === persona.id
        return (
          <WaydTooltip title={persona.description} key={persona.id}>
            <Button
              key={persona.id}
              size="small"
              variant="outlined"
              color={isSelected ? 'primary' : 'default'}
              icon={<ColorDot color={persona.color} />}
              onClick={() => onSelectPersona(isSelected ? null : persona.id)}
            >
              {persona.name}
            </Button>
          </WaydTooltip>
        )
      })}

      {canUpdate &&
        (isQuickAdding ? (
          <Input
            ref={inputRef}
            size="small"
            className={styles.personaQuickAddInput}
            placeholder="Persona name"
            maxLength={128}
            value={quickAddName}
            onChange={(e) => setQuickAddName(e.target.value)}
            onKeyDown={handleKeyDown}
            onBlur={commitQuickAdd}
          />
        ) : (
          <Button
            size="small"
            type="dashed"
            icon={<PlusOutlined />}
            data-tour="add-persona"
            onClick={startQuickAdd}
          >
            Persona
          </Button>
        ))}

      {canUpdate && (
        <Button
          type="text"
          size="small"
          icon={<SettingOutlined />}
          onClick={onManage}
        >
          Manage
        </Button>
      )}

      <div className={styles.personaBarSummary}>
        <CountItem value={counts.goals} singular="goal" />
        <CountItem value={counts.steps} singular="step" />
        <CountItem value={counts.tasks} singular="task" />
      </div>
    </Flex>
  )
}

export default PersonaFilterBar
