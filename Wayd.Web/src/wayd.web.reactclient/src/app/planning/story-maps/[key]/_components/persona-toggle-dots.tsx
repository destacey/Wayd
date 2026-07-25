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
 * One dot per persona on the map, shown in a step or task footer. A dot is filled with the persona's
 * colour when linked and hollow when not, and clicking it toggles the link. Hovering names the
 * persona, since colour alone does not identify it — filled vs hollow already says which way a
 * click will go, so the tooltip is the name only.
 */
const PersonaToggleDots: FC<PersonaToggleDotsProps> = ({
  personas,
  linkedPersonaIds,
  disabled,
  onToggle,
}) => {
  if (personas.length === 0) return null

  const handleClick = (e: MouseEvent, personaId: string) => {
    // The whole cell is click-to-edit (and will become a drag handle); a dot click is neither.
    e.stopPropagation()
    onToggle(personaId)
  }

  return (
    <span className={styles.personaDots}>
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
