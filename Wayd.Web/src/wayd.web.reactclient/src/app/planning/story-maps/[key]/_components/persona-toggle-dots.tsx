'use client'

import { StoryMapPersonaDto } from '@/src/services/wayd-api'
import { WaydTooltip } from '@/src/components/common'
import { FC, MouseEvent } from 'react'
import styles from '../../_components/story-map.module.css'

export interface PersonaToggleDotsProps {
  /** Every persona on the map — an unlinked one renders hollow so it can be clicked to link. */
  personas: StoryMapPersonaDto[]
  /** Ids currently linked to this step or task. */
  linkedPersonaIds: string[]
  disabled: boolean
  onToggle: (personaId: string) => void
}

/**
 * One dot per persona on the map, shown in a step or task footer: filled with the persona's colour
 * when linked, hollow when not, and clicking toggles the link. Hovering names the persona.
 */
const PersonaToggleDots: FC<PersonaToggleDotsProps> = ({
  personas,
  linkedPersonaIds,
  disabled,
  onToggle,
}) => {
  if (personas.length === 0) return null

  const handleClick = (e: MouseEvent, personaId: string) => {
    // The whole cell is a drag surface and click-to-edit; a dot click is neither.
    e.stopPropagation()
    onToggle(personaId)
  }

  return (
    <span className={styles.personaDots} data-tour="persona-dots">
      {personas.map((persona) => {
        const isLinked = linkedPersonaIds.includes(persona.id)
        return (
          <WaydTooltip key={persona.id} title={persona.name}>
            <button
              type="button"
              className={`${styles.personaDot} ${styles.personaDotToggle} ${
                isLinked ? '' : styles.personaDotUnlinked
              }`}
              style={isLinked ? { backgroundColor: persona.color } : undefined}
              disabled={disabled}
              aria-pressed={isLinked}
              aria-label={persona.name}
              onClick={(e) => handleClick(e, persona.id)}
            />
          </WaydTooltip>
        )
      })}
    </span>
  )
}

export default PersonaToggleDots
