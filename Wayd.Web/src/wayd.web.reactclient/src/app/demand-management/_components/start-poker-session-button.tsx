'use client'

import { IntakeDeskDto, IntakeRequestSummaryDto } from '@/src/services/wayd-api'
import { Button, Tooltip } from 'antd'
import { useRouter } from 'next/navigation'
import { FC, useState } from 'react'
import { pokerSelection, pokerSessionHref } from './desk-poker'
import DeskPokerSessionForm from './desk-poker-session-form'

export interface StartPokerSessionButtonProps {
  /** The requests selected in the list. */
  selected: IntakeRequestSummaryDto[]
  desks: IntakeDeskDto[] | undefined
  employeeId: string | null | undefined
  /** Called once the session has started, so the list can clear its selection. */
  onStarted?: () => void
}

/**
 * Starts a poker session with the requests selected in a list, then opens it.
 * Disabled, saying why, until the selection is open requests on one desk the
 * reader works.
 */
const StartPokerSessionButton: FC<StartPokerSessionButtonProps> = ({
  selected,
  desks,
  employeeId,
  onStarted,
}) => {
  const router = useRouter()
  const [open, setOpen] = useState(false)
  const selection = pokerSelection(selected, desks, employeeId)
  const refusal = 'refusal' in selection ? selection.refusal : undefined

  return (
    <>
      <Tooltip title={refusal}>
        <Button size="small" disabled={!!refusal} onClick={() => setOpen(true)}>
          Estimate in Poker
          {selected.length > 0 ? ` (${selected.length})` : ''}
        </Button>
      </Tooltip>
      {open && 'desk' in selection && (
        <DeskPokerSessionForm
          desk={selection.desk}
          initialRequestIds={selection.requestIds}
          onFormComplete={(started) => {
            setOpen(false)
            onStarted?.()
            if (started) router.push(pokerSessionHref(started.key))
          }}
          onFormCancel={() => setOpen(false)}
        />
      )}
    </>
  )
}

export default StartPokerSessionButton
