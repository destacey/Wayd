import { WorkIterationNavigationDto } from '@/src/services/wayd-api'
import Link from 'next/link'

export interface SprintLinkProps {
  sprint: WorkIterationNavigationDto | undefined | null
  showTeamCode?: boolean
}

const SprintLink = ({ sprint, showTeamCode = true }: SprintLinkProps) => {
  if (!sprint) return null

  const url = `/work/sprints/${sprint.key}`
  const displayText =
    showTeamCode && sprint.team?.code
      ? `${sprint.name} (${sprint.team.code})`
      : sprint.name

  return <Link href={url}>{displayText}</Link>
}

export default SprintLink

