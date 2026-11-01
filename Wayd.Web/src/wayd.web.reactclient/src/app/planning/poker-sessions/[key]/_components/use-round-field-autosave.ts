'use client'

import { RefObject, useEffect, useRef, useState } from 'react'

const DEBOUNCE_MS = 600

/** An edit typed but not yet sent, with the round and save it belongs to. */
interface PendingEdit {
  roundId: string
  value: string
  save: (roundId: string, value: string) => void
}

/** Sends the pending edit, if any, to the round it was typed for. */
const flushPending = (
  pendingRef: RefObject<PendingEdit | null>,
  timerRef: RefObject<ReturnType<typeof setTimeout> | null>,
): boolean => {
  if (timerRef.current) {
    clearTimeout(timerRef.current)
    timerRef.current = null
  }
  const pending = pendingRef.current
  if (!pending) return false
  pendingRef.current = null
  pending.save(pending.roundId, pending.value.trim())
  return true
}

export interface RoundFieldAutosaveOptions {
  roundId: string
  /** The field as the server last returned it. */
  serverValue: string | undefined
  /** Persists the trimmed value; called once per pause in typing, on blur, and before the field goes away. */
  save: (roundId: string, value: string) => void
}

/**
 * A round's text field that saves as the user types.
 *
 * Every save comes back as a refresh (the mutation's invalidation and the session's SignalR event), so
 * a server value is taken only when it is someone else's change: never over edits not yet sent, and
 * never when it is just the trimmed echo of what this field sent, which would strip a trailing space
 * mid-word. An edit still waiting when the round changes or the field unmounts is sent, not dropped.
 */
export const useRoundFieldAutosave = ({
  roundId,
  serverValue,
  save,
}: RoundFieldAutosaveOptions) => {
  const [value, setValue] = useState(serverValue ?? '')
  const [hasUnsentEdits, setHasUnsentEdits] = useState(false)
  const [synced, setSynced] = useState({ roundId, serverValue })
  const pendingRef = useRef<PendingEdit | null>(null)
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  if (synced.roundId !== roundId) {
    setSynced({ roundId, serverValue })
    setValue(serverValue ?? '')
    setHasUnsentEdits(false)
  } else if (synced.serverValue !== serverValue) {
    setSynced({ roundId, serverValue })
    if (!hasUnsentEdits && (serverValue ?? '') !== value.trim()) {
      setValue(serverValue ?? '')
    }
  }

  // The cleanup runs when the round changes and on unmount, sending the edit for the round it was typed on.
  useEffect(() => {
    return () => {
      flushPending(pendingRef, timerRef)
    }
  }, [roundId])

  const onChange = (next: string) => {
    setValue(next)
    setHasUnsentEdits(true)
    pendingRef.current = { roundId, value: next, save }
    if (timerRef.current) clearTimeout(timerRef.current)
    timerRef.current = setTimeout(() => {
      if (flushPending(pendingRef, timerRef)) setHasUnsentEdits(false)
    }, DEBOUNCE_MS)
  }

  const onBlur = () => {
    if (flushPending(pendingRef, timerRef)) setHasUnsentEdits(false)
  }

  return { value, onChange, onBlur }
}
