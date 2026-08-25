'use client'

import { useAppDispatch, useDocumentTitle } from '@/src/hooks'
import { getAvatarColor } from '@/src/utils'
import { usePokerSessionConnection } from '@/src/hooks/use-poker-session-connection'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import useAuth from '@/src/components/contexts/auth'
import useTheme from '@/src/components/contexts/theme'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  useGetPokerSessionQuery,
  useCompletePokerSessionMutation,
  useAddPokerRoundMutation,
  useRemovePokerRoundMutation,
  useSubmitPokerVoteMutation,
  useWithdrawPokerVoteMutation,
} from '@/src/store/features/planning/poker-sessions-api'
import { useGetProfileQuery } from '@/src/store/features/user-management/profile-api'
import { Avatar, Button, Divider, Drawer, Flex, Grid, Tag } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import { EditOutlined, LinkOutlined } from '@ant-design/icons'
import { notFound, useParams, usePathname } from 'next/navigation'
import {
  CSSProperties,
  FC,
  useEffect,
  useState,
} from 'react'
import { PresenceParticipant } from '@/src/hooks/use-poker-session-connection'
import PageTitle from '@/src/components/common/page-title'
import { setBreadcrumbTitle } from '@/src/store/breadcrumbs'
import styles from './_components/poker-session.module.css'
import {
  EditPokerSessionForm,
  EstimationCardDeck,
  LobbyParticipants,
  ParticipantCards,
  PokerLobbyState,
  PokerReviewView,
  ResultsPanel,
  RoundLabelHeader,
  SessionSidebar,
  VotingActions,
} from './_components'
import PokerSessionDetailsLoading from './loading'

const { Group: AvatarGroup } = Avatar
const { useBreakpoint } = Grid

interface PokerCssVars extends CSSProperties {
  '--poker-bg': string
  '--poker-border': string
  '--poker-text': string
  '--poker-text-secondary': string
  '--poker-muted': string
  '--poker-primary': string
  '--poker-primary-bg': string
  '--poker-success': string
  '--poker-active-bg': string
  '--poker-hover-bg': string
  '--poker-warning': string
  '--poker-radius-lg': string
}

const PokerSessionDetailPage: FC = () => {
  const { key } = useParams<{ key: string }>()
  const pathname = usePathname()
  const dispatch = useAppDispatch()
  useDocumentTitle(`Poker Session ${key}`)

  const { token } = useTheme()
  const screens = useBreakpoint()
  const isMobile = !screens.md
  const messageApi = useMessage()
  const { hasPermissionClaim } = useAuth()
  const canManage = hasPermissionClaim('Permissions.PokerSessions.Update')

  const { data: session, isLoading } = useGetPokerSessionQuery(key)
  const { data: profile } = useGetProfileQuery()

  useEffect(() => {
    if (!session) return
    dispatch(setBreadcrumbTitle({ title: session.name, pathname }))
  }, [dispatch, pathname, session])

  const [connectedParticipants, setConnectedParticipants] = useState<
    PresenceParticipant[]
  >([])

  // Real-time updates via SignalR (falls back to polling above)
  // Skip connection for completed sessions — no live updates needed.
  const isSessionActive = session?.status === 'Active'
  usePokerSessionConnection(
    isSessionActive ? session?.id : undefined,
    isSessionActive ? session?.key : undefined,
    setConnectedParticipants,
  )

  const [completeSession, { isLoading: isCompleting }] =
    useCompletePokerSessionMutation()
  const [addRound] = useAddPokerRoundMutation()
  const [removeRound] = useRemovePokerRoundMutation()
  const [submitVote, { isLoading: isSubmitting }] = useSubmitPokerVoteMutation()
  const [withdrawVote, { isLoading: isWithdrawing }] =
    useWithdrawPokerVoteMutation()

  const [selectedRoundId, setSelectedRoundId] = useState<string | undefined>()
  const [openEditForm, setOpenEditForm] = useState(false)
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false)

  const handleComplete = async () => {
    if (!session) return
    try {
      const response = await completeSession({
        id: session.id,
        key: session.key,
      })
      if (response.error) throw response.error
      messageApi.success('Session completed.')
    } catch {
      messageApi.error('Failed to complete session.')
    }
  }

  const handleRemoveRound = async (roundId: string) => {
    if (!session) return
    try {
      const response = await removeRound({
        sessionId: session.id,
        roundId,
        sessionKey: session.key,
      })
      if (response.error) throw response.error
      if (selectedRoundId === roundId) {
        setSelectedRoundId(undefined)
      }
    } catch {
      messageApi.error('Failed to remove round.')
    }
  }

  const handleCopyInviteLink = () => {
    navigator.clipboard.writeText(window.location.href)
    messageApi.success('Invite link copied to clipboard.')
  }

  const rounds = session?.rounds ?? []
  const activeRound = selectedRoundId
    ? rounds.find((r) => r.id === selectedRoundId)
    : (rounds.find((r) => r.status === 'Voting' || r.status === 'Revealed') ??
      rounds[0])

  const handleConsensusSet = async () => {
    if (!session) return
    const hasNextVotingRound = rounds.some(
      (r) => r.id !== activeRound?.id && r.status === 'Voting',
    )
    if (!hasNextVotingRound) {
      try {
        await addRound({
          sessionId: session.id,
          sessionKey: session.key,
          request: { label: '' },
        })
      } catch {
        // non-critical — the round list will still refresh via SignalR/polling
      }
    }
    setSelectedRoundId(undefined)
  }

  // Determine center column view mode
  const centerViewMode = !activeRound
    ? ('lobby' as const)
    : activeRound.status === 'Accepted'
      ? ('review' as const)
      : ('active' as const)

  // Merge connected participants with voters from the active round
  // so that votes from disconnected users still appear
  const roundParticipants = (() => {
    const map = new Map<string, { id: string; name: string }>()
    for (const p of connectedParticipants) {
      map.set(p.id, p)
    }
    if (activeRound) {
      for (const vote of activeRound.votes ?? []) {
        if (vote.participant && !map.has(vote.participant.id)) {
          map.set(vote.participant.id, {
            id: vote.participant.id,
            name: vote.participant.name!,
          })
        }
      }
    }
    return Array.from(map.values())
  })()

  // Derive the current user's vote from the API data
  const selectedVote = (() => {
    if (!activeRound || !profile?.id) return undefined
    const myVote = activeRound.votes?.find(
      (v) => v.participant?.id === profile.id,
    )
    return myVote?.value || undefined
  })()

  const handleVote = async (value: string) => {
    if (!session || !activeRound) return
    try {
      if (selectedVote === value) {
        const response = await withdrawVote({
          sessionId: session.id,
          roundId: activeRound.id,
          sessionKey: session.key,
        })
        if (response.error) throw response.error
      } else {
        const response = await submitVote({
          sessionId: session.id,
          roundId: activeRound.id,
          sessionKey: session.key,
          request: { value },
        })
        if (response.error) throw response.error
      }
    } catch {
      messageApi.error('Failed to submit vote.')
    }
  }

  if (isLoading) {
    return <PokerSessionDetailsLoading />
  }

  if (!session) {
    return notFound()
  }

  const isActive = session.status === 'Active'

  const cssVars: PokerCssVars = {
    '--poker-bg': token.colorBgContainer,
    '--poker-border': token.colorBorderSecondary,
    '--poker-text': token.colorText,
    '--poker-text-secondary': token.colorTextSecondary,
    '--poker-muted': token.colorTextTertiary,
    '--poker-primary': token.colorPrimary,
    '--poker-primary-bg': token.colorPrimaryBg,
    '--poker-success': token.colorSuccess,
    '--poker-active-bg': token.controlItemBgActive,
    '--poker-hover-bg': token.controlItemBgHover,
    '--poker-warning': token.colorWarning,
    '--poker-radius-lg': `${token.borderRadiusLG}px`,
  }

  const sessionStatusColor: Record<string, string> = {
    Active: 'processing',
    Completed: 'success',
  }

  const pageTitleTags = (
    <Flex align="center" gap={8} wrap>
      <Tag color={sessionStatusColor[session.status]}>{session.status}</Tag>
      {session.estimationScale?.name && (
        <Tag style={{ margin: 0 }}>{session.estimationScale.name}</Tag>
      )}
    </Flex>
  )

  const pageTitleActions = (
    <Flex align="center" gap={8} wrap>
      {canManage && isActive && (
        <Button icon={<EditOutlined />} onClick={() => setOpenEditForm(true)}>
          Edit
        </Button>
      )}
      {isActive && (
        <Button icon={<LinkOutlined />} onClick={handleCopyInviteLink}>
          Copy Invite Link
        </Button>
      )}
      {connectedParticipants.length > 0 && (
        <AvatarGroup
          max={{
            count: 5,
            style: { backgroundColor: token.colorPrimary, fontSize: 12 },
          }}
          size="small"
        >
          {connectedParticipants.map((p) => (
            <WaydTooltip key={p.id} title={p.name}>
              <Avatar
                size="small"
                style={{ backgroundColor: getAvatarColor(p.id) }}
              >
                {p.name.charAt(0).toUpperCase()}
              </Avatar>
            </WaydTooltip>
          ))}
        </AvatarGroup>
      )}
    </Flex>
  )

  return (
    <div className={styles.pageContainer} style={cssVars}>
      <PageTitle
        title={session.name}
        subtitle="Planning Poker"
        tags={pageTitleTags}
        actions={pageTitleActions}
      />

      <div
        className={`${styles.pageLayout} ${isMobile ? styles.pageLayoutMobile : ''}`}
      >
        <div className={styles.centerColumn}>
          {centerViewMode === 'lobby' && (
            <PokerLobbyState
              isActive={isActive}
              canManage={canManage}
              sessionId={session.id}
              sessionKey={session.key}
            />
          )}

          {centerViewMode === 'active' && activeRound && (
            <Flex
              vertical
              gap={16}
              align="center"
              style={{ padding: isMobile ? '12px 0' : '24px 0' }}
            >
              <RoundLabelHeader
                round={activeRound}
                sessionId={session.id}
                sessionKey={session.key}
                canManage={canManage && isActive}
              />

              <div className={styles.votingCardPanel}>
                <ParticipantCards
                  participants={roundParticipants}
                  votes={activeRound.votes}
                  isRevealed={
                    activeRound.status === 'Revealed' ||
                    activeRound.status === 'Accepted'
                  }
                />

                <VotingActions
                  round={activeRound}
                  sessionId={session.id}
                  sessionKey={session.key}
                  estimationScaleValues={session.estimationScale?.values ?? []}
                  canManage={canManage && isActive}
                  onConsensusSet={handleConsensusSet}
                />
              </div>

              {(activeRound.status === 'Revealed' ||
                activeRound.status === 'Accepted') && (
                <ResultsPanel votes={activeRound.votes} />
              )}

              {activeRound.status === 'Voting' && (
                <>
                  <Divider style={{ margin: '8px 0' }}>
                    <span className={styles.yourEstimateLabel}>
                      Your Estimate
                    </span>
                  </Divider>
                  <EstimationCardDeck
                    values={session.estimationScale?.values ?? []}
                    selectedValue={selectedVote}
                    onSelect={handleVote}
                    disabled={isSubmitting || isWithdrawing}
                  />
                </>
              )}
            </Flex>
          )}

          {centerViewMode === 'review' && activeRound && (
            <PokerReviewView
              round={activeRound}
              sessionId={session.id}
              sessionKey={session.key}
              canManage={canManage && isActive}
              participants={roundParticipants}
            />
          )}

          {isMobile && centerViewMode !== 'lobby' && (
            <div style={{ padding: '12px 0' }}>
              <Button block onClick={() => setMobileSidebarOpen(true)}>
                Session Timeline
              </Button>
            </div>
          )}
        </div>

        {isMobile ? (
          <>
            {centerViewMode === 'lobby' && isActive && (
              <LobbyParticipants
                participants={connectedParticipants}
                canManage={canManage}
                isActive={isActive}
                onComplete={handleComplete}
                isCompleting={isCompleting}
              />
            )}
            <Drawer
              title="Session Timeline"
              placement="bottom"
              size="70vh"
              onClose={() => setMobileSidebarOpen(false)}
              open={mobileSidebarOpen}
              styles={{ body: { padding: 0 } }}
            >
              <SessionSidebar
                session={session}
                selectedRoundId={activeRound?.id}
                onSelectRound={(roundId) => {
                  setSelectedRoundId(roundId)
                  setMobileSidebarOpen(false)
                }}
                onRemoveRound={handleRemoveRound}
                onComplete={handleComplete}
                isCompleting={isCompleting}
                canManage={canManage}
                isActive={isActive}
                inline
              />
            </Drawer>
          </>
        ) : (
          <>
            {centerViewMode === 'lobby' && isActive ? (
              <LobbyParticipants
                participants={connectedParticipants}
                canManage={canManage}
                isActive={isActive}
                onComplete={handleComplete}
                isCompleting={isCompleting}
              />
            ) : centerViewMode !== 'lobby' ? (
              <SessionSidebar
                session={session}
                selectedRoundId={activeRound?.id}
                onSelectRound={setSelectedRoundId}
                onRemoveRound={handleRemoveRound}
                onComplete={handleComplete}
                isCompleting={isCompleting}
                canManage={canManage}
                isActive={isActive}
              />
            ) : null}
          </>
        )}
      </div>
      {openEditForm && (
        <EditPokerSessionForm
          sessionKey={session.key}
          onFormUpdate={() => setOpenEditForm(false)}
          onFormCancel={() => setOpenEditForm(false)}
        />
      )}
    </div>
  )
}

const PokerSessionDetailPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    PokerSessionDetailPage,
    'Permission',
    'Permissions.PokerSessions.View',
  ),
  'planning-poker',
)

export default PokerSessionDetailPageWithAuthorization
