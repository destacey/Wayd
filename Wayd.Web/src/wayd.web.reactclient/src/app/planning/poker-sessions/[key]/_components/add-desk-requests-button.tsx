'use client'

import DeskPokerSessionForm from '@/src/app/demand-management/_components/desk-poker-session-form'
import {
  PokerRoundSubjectKind,
  PokerSessionDetailsDto,
  PokerSessionHostKind,
} from '@/src/services/wayd-api'
import { Button } from 'antd'
import { FC, useState } from 'react'

/** The intake desk hosting the session, or undefined for any other session. */
export const hostDeskId = (
  session: PokerSessionDetailsDto,
): string | undefined =>
  session.host?.kind === PokerSessionHostKind.IntakeDesk
    ? session.host.id
    : undefined

export interface AddDeskRequestsButtonProps {
  session: PokerSessionDetailsDto
  block?: boolean
}

/**
 * Adds rounds about more of the hosting desk's open requests. Renders nothing
 * for a session no intake desk hosts. Offer it only to someone who runs an
 * active session.
 */
const AddDeskRequestsButton: FC<AddDeskRequestsButtonProps> = ({
  session,
  block,
}) => {
  const [open, setOpen] = useState(false)
  const deskId = hostDeskId(session)
  if (!deskId) return null

  return (
    <>
      <Button block={block} onClick={() => setOpen(true)}>
        Add requests
      </Button>
      {open && (
        <DeskPokerSessionForm
          desk={{ id: deskId, name: 'the desk' }}
          session={{ id: session.id }}
          excludedRequestIds={(session.rounds ?? [])
            .filter(
              (r) => r.subject?.kind === PokerRoundSubjectKind.IntakeRequest,
            )
            .map((r) => r.subject!.id)}
          onFormComplete={() => setOpen(false)}
          onFormCancel={() => setOpen(false)}
        />
      )}
    </>
  )
}

export default AddDeskRequestsButton
