import {
  ImportProcessRowDto,
  ImportRecordLink,
  ImportRecordType,
} from '@/src/services/wayd-api'

const segment = (value: string) => encodeURIComponent(value)

const ROUTES: Record<ImportRecordType, (link: ImportRecordLink) => string> = {
  [ImportRecordType.Employee]: (l) =>
    `/organizations/employees/${segment(l.key)}`,
  [ImportRecordType.Team]: (l) => `/organizations/teams/${segment(l.key)}`,
  [ImportRecordType.TeamOfTeams]: (l) =>
    `/organizations/team-of-teams/${segment(l.key)}`,
  [ImportRecordType.PlanningInterval]: (l) =>
    `/planning/planning-intervals/${segment(l.key)}`,
  [ImportRecordType.PlanningIntervalObjective]: (l) =>
    `/planning/planning-intervals/${segment(l.parentKey ?? '')}/objectives/${segment(l.key)}`,
  [ImportRecordType.Risk]: (l) => `/planning/risks/${segment(l.key)}`,
  [ImportRecordType.Portfolio]: (l) => `/ppm/portfolios/${segment(l.key)}`,
  [ImportRecordType.Program]: (l) => `/ppm/programs/${segment(l.key)}`,
  [ImportRecordType.Project]: (l) => `/ppm/projects/${segment(l.key)}`,
  [ImportRecordType.StrategicInitiative]: (l) =>
    `/ppm/strategic-initiatives/${segment(l.key)}`,
  [ImportRecordType.StrategicTheme]: (l) =>
    `/strategic-management/strategic-themes/${segment(l.key)}`,
  [ImportRecordType.Product]: (l) =>
    `/product-management/products/${segment(l.key)}`,
  [ImportRecordType.Version]: (l) =>
    `/product-management/versions/${segment(l.key)}`,
  [ImportRecordType.Release]: (l) =>
    `/product-management/releases/${segment(l.key)}`,
  [ImportRecordType.ReleasePackage]: (l) =>
    `/product-management/release-packages/${segment(l.key)}`,
  [ImportRecordType.Deployment]: (l) =>
    `/product-management/deployments/${segment(l.key)}`,
  // Environments have no page of their own, only the list.
  [ImportRecordType.DeploymentEnvironment]: () =>
    '/product-management/environments',
}

/**
 * The page that shows what an import row did. For a record with no page of its own — a membership, a
 * dependency, a task — the server has already pointed the link at the record it belongs to.
 */
export const importRecordHref = (
  link: ImportRecordLink,
): string | undefined => {
  if (
    link.type === ImportRecordType.PlanningIntervalObjective &&
    !link.parentKey
  )
    return undefined

  return ROUTES[link.type]?.(link)
}

/** The key, then the name when there is one, so the link is recognisable without opening it. */
export const importRecordLabel = (link: ImportRecordLink): string =>
  link.name ? `${link.key} - ${link.name}` : link.key

/**
 * What a row's Wayd Record column says, as text — the cell, the column filter and the CSV export all read
 * it, so a deleted record exports the way it shows. A row that reports a created id the server could not
 * link is one whose record has since been deleted.
 */
export const importRecordText = (
  row: Pick<ImportProcessRowDto, 'createdRecord' | 'createdEntityId'>,
): string => {
  if (row.createdRecord) return importRecordLabel(row.createdRecord)
  return row.createdEntityId ? 'No longer exists' : ''
}
