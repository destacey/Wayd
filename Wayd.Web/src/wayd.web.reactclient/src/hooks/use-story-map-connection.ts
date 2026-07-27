import { useCallback, useEffect, useRef } from 'react'
import {
  HubConnectionBuilder,
  HubConnection,
  LogLevel,
} from '@microsoft/signalr'
import { getFreshAuthToken } from '@/src/services/clients'
import { store } from '@/src/store/store'
import { storyMapsApi } from '@/src/store/features/planning/story-maps-api'
import { QueryTags } from '@/src/store/features/query-tags'

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? ''

export interface PresenceParticipant {
  id: string
  name: string
}

/**
 * Establishes a SignalR connection to the Story Map hub and:
 * - tracks who else is present on the map (presence), and
 * - invalidates the map's RTK Query cache when a change event arrives, so every
 *   viewer's board stays current in real time.
 *
 * Mirrors the Planning Poker connection hook. Falls back gracefully if the hub
 * is unavailable (e.g. local dev without SignalR configured).
 *
 * The change events are granular and typed (GoalAdded, TaskMoved, …); for this
 * release the client responds by refetching the map graph on any of them. The
 * event payloads are carried so a future revision can patch the cache in place.
 */
export function useStoryMapConnection(
  storyMapId: string | undefined,
  onPresenceChange?: (participants: PresenceParticipant[]) => void,
  onMapDeleted?: () => void,
) {
  const connectionRef = useRef<HubConnection | null>(null)
  const presenceMapRef = useRef(new Map<string, PresenceParticipant>())
  const onPresenceChangeRef = useRef(onPresenceChange)
  const onMapDeletedRef = useRef(onMapDeleted)

  useEffect(() => {
    onPresenceChangeRef.current = onPresenceChange
  }, [onPresenceChange])

  useEffect(() => {
    onMapDeletedRef.current = onMapDeleted
  }, [onMapDeleted])

  const emitPresence = () => {
    onPresenceChangeRef.current?.(Array.from(presenceMapRef.current.values()))
  }

  const invalidateMap = useCallback(() => {
    if (!storyMapId) return
    store.dispatch(
      storyMapsApi.util.invalidateTags([
        { type: QueryTags.StoryMap, id: storyMapId },
      ]),
    )
  }, [storyMapId])

  useEffect(() => {
    if (!storyMapId) return

    let cancelled = false

    const connect = async () => {
      try {
        const connection = new HubConnectionBuilder()
          .withUrl(`${API_BASE_URL}/hubs/story-maps`, {
            // SignalR authenticates via the Wayd JWT, same as REST. The factory
            // runs on every connect/reconnect, so refresh-before-read to survive
            // token expiry; getFreshAuthToken shares the single-flight refresh
            // with the axios interceptor.
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

        // Any granular change to the map refreshes the local copy.
        const changeEvents = [
          'MapUpdated',
          'MapArchived',
          'GoalAdded',
          'GoalRenamed',
          'GoalReordered',
          'GoalDeleted',
          'StepAdded',
          'StepRenamed',
          'StepReordered',
          'StepMoved',
          'StepDeleted',
          'TaskAdded',
          'TaskUpdated',
          'TaskMoved',
          'TaskDeleted',
          'TaskPersonasChanged',
          'TaskChecklistChanged',
          'ChecklistItemPromoted',
          'TaskWorkItemLinked',
          'TaskWorkItemUnlinked',
          'SwimLaneAdded',
          'SwimLaneRenamed',
          'SwimLaneDatesChanged',
          'SwimLaneReordered',
          'SwimLaneRemoved',
          'PersonaAdded',
          'PersonaUpdated',
          'PersonaDeleted',
          'PersonaReordered',
          'GoalPersonasChanged',
          'StepPersonasChanged',
        ]
        for (const event of changeEvents) {
          connection.on(event, invalidateMap)
        }

        // Deletion is not a change to refetch — the map is gone, and refetching would leave the
        // viewer on a broken board. The page decides where to send them.
        connection.on('MapDeleted', () => {
          onMapDeletedRef.current?.()
        })

        // Presence events
        connection.on(
          'ParticipantList',
          (participants: { id: string; name: string }[]) => {
            presenceMapRef.current.clear()
            for (const p of participants) {
              presenceMapRef.current.set(p.id, { id: p.id, name: p.name })
            }
            emitPresence()
          },
        )

        connection.on(
          'ParticipantJoined',
          (participant: { id: string; name: string }) => {
            presenceMapRef.current.set(participant.id, {
              id: participant.id,
              name: participant.name,
            })
            emitPresence()
          },
        )

        connection.on('ParticipantLeft', (data: { id: string }) => {
          presenceMapRef.current.delete(data.id)
          emitPresence()
        })

        // A reconnect gets a new connection id and the server drops the old one from the group, so
        // without re-joining the board looks live while receiving nothing. Refetch to catch up.
        connection.onreconnected(() => {
          if (cancelled) return
          connection
            .invoke('JoinMap', storyMapId)
            .then(invalidateMap)
            .catch((error) => {
              console.warn('Story Map rejoin after reconnect failed:', error)
            })
        })

        // Reconnection gives up eventually; drop the avatars rather than leave them frozen.
        connection.onclose(() => {
          if (cancelled) return
          presenceMapRef.current.clear()
          emitPresence()
        })

        await connection.start()

        if (cancelled) {
          await connection.stop()
          return
        }

        // Join the map group — triggers the ParticipantList event.
        await connection.invoke('JoinMap', storyMapId)
        connectionRef.current = connection
      } catch (error) {
        // SignalR is optional — the page still works without live updates.
        console.warn(
          'Story Map SignalR connection failed; continuing without live updates:',
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
          .invoke('LeaveMap', storyMapId)
          .catch(() => {})
          .finally(() => conn.stop())
        connectionRef.current = null
      }
    }
  }, [storyMapId, invalidateMap])
}
