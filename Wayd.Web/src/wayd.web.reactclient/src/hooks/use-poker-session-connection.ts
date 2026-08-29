'use client'

import { useCallback, useEffect, useRef } from 'react'
import {
  HubConnectionBuilder,
  HubConnection,
  LogLevel,
} from '@microsoft/signalr'
import { getFreshAuthToken } from '@/src/services/clients'
import { store } from '@/src/store/store'
import { pokerSessionsApi } from '@/src/store/features/planning/poker-sessions-api'
import { QueryTags } from '@/src/store/features/query-tags'

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? ''

export interface PresenceParticipant {
  id: string
  name: string
  /** Absent when the account is not linked to an employee. */
  employeeId?: string
}

/**
 * Establishes a SignalR connection to the Planning Poker hub and
 * automatically invalidates RTK Query cache tags when events arrive.
 * Also tracks participant presence via hub events.
 *
 * Falls back gracefully if the hub is not available (e.g., during local dev
 * without SignalR configured). The detail page also uses 5s polling as a
 * secondary fallback.
 */
export function usePokerSessionConnection(
  sessionId: string | undefined,
  sessionKey: number | undefined,
  onPresenceChange?: (participants: PresenceParticipant[]) => void,
) {
  const connectionRef = useRef<HubConnection | null>(null)
  const presenceMapRef = useRef(new Map<string, PresenceParticipant>())
  const onPresenceChangeRef = useRef(onPresenceChange)

  useEffect(() => {
    onPresenceChangeRef.current = onPresenceChange
  }, [onPresenceChange])

  const emitPresence = () => {
    onPresenceChangeRef.current?.(
      Array.from(presenceMapRef.current.values()),
    )
  }

  const invalidateSession = useCallback(() => {
    if (sessionKey === undefined) return
    store.dispatch(
      pokerSessionsApi.util.invalidateTags([
        { type: QueryTags.PokerSessionRound, id: sessionKey },
      ]),
    )
  }, [sessionKey])

  useEffect(() => {
    if (!sessionId) return

    let cancelled = false

    const connect = async () => {
      try {
        const connection = new HubConnectionBuilder()
          .withUrl(`${API_BASE_URL}/hubs/planning-poker`, {
            // SignalR authenticates via the Wayd JWT, same as REST. The hub's
            // [Authorize] resolves the LocalJwt scheme off ?access_token=
            // (see Local/ConfigureServices.cs OnMessageReceived).
            //
            // SignalR calls this factory on every connect and reconnect, so we
            // must refresh-before-read to survive token expiry across long-
            // running sessions. getFreshAuthToken shares the single-flight
            // refresh with the axios interceptor — no duplicate refresh calls.
            accessTokenFactory: async () => (await getFreshAuthToken()) ?? '',
            // In prod the negotiate redirects the browser to Azure SignalR
            // Service, which does not support credentialed CORS — with the
            // signalr default (withCredentials: true) its preflight response
            // has no Access-Control-Allow-Origin and the connect fails. Auth
            // is Bearer-only (accessTokenFactory above), no cookies needed.
            withCredentials: false,
          })
          .withAutomaticReconnect()
          .configureLogging(LogLevel.Warning)
          .build()

        // Game events trigger cache invalidation so the UI refreshes
        const events = [
          'VoteSubmitted',
          'VoteWithdrawn',
          'VotesRevealed',
          'ConsensusSet',
          'RoundReset',
          'SessionUpdated',
          'SessionCompleted',
          'RoundAdded',
          'RoundRemoved',
          'RoundLabelUpdated',
        ]
        for (const event of events) {
          connection.on(event, invalidateSession)
        }

        // Presence events
        connection.on(
          'ParticipantList',
          (participants: { id: string; name: string; employeeId?: string }[]) => {
            presenceMapRef.current.clear()
            for (const p of participants) {
              presenceMapRef.current.set(p.id, {
                id: p.id,
                name: p.name,
                employeeId: p.employeeId,
              })
            }
            emitPresence()
          },
        )

        connection.on(
          'ParticipantJoined',
          (participant: { id: string; name: string; employeeId?: string }) => {
            presenceMapRef.current.set(participant.id, {
              id: participant.id,
              name: participant.name,
              employeeId: participant.employeeId,
            })
            emitPresence()
          },
        )

        connection.on('ParticipantLeft', (data: { id: string }) => {
          presenceMapRef.current.delete(data.id)
          emitPresence()
        })

        await connection.start()

        if (cancelled) {
          await connection.stop()
          return
        }

        // Join the session group — triggers ParticipantList event
        await connection.invoke('JoinSession', sessionId)
        connectionRef.current = connection
      } catch (error) {
        // SignalR is optional — polling is the fallback
        console.warn(
          'Planning Poker SignalR connection failed, falling back to polling:',
          error,
        )
      }
    }

    connect()

    const presenceMap = presenceMapRef.current
    return () => {
      cancelled = true
      presenceMap.clear()
      const conn = connectionRef.current
      if (conn) {
        conn
          .invoke('LeaveSession', sessionId)
          .catch(() => {})
          .finally(() => conn.stop())
        connectionRef.current = null
      }
    }
  }, [sessionId, sessionKey, invalidateSession])
}
