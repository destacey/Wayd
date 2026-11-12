import styles from './incidents.module.css'

type Props = {
  name: string
  color: string
  className?: string
}

export function IncidentSeverityRank({ name, color, className }: Props) {
  return (
    <span className={[styles.rank, className].filter(Boolean).join(' ')}>
      <b style={{ background: color }} aria-hidden />
      {name}
    </span>
  )
}
