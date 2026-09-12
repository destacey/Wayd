import {
  ImportAtomicity,
  ImportProcessDto,
  ImportProcessStatus,
} from '@/src/services/wayd-api'
import { buildImportRows, groupLabel } from './import-groups'

const GROUP = '6cca310c-0d9e-4a29-a6ad-38f13c44481a'

let nextId = 1

const run = (overrides: Partial<ImportProcessDto> = {}): ImportProcessDto => {
  const id = `run-${nextId++}`
  return {
    id,
    importType: 'employees',
    displayName: 'Employees',
    atomicity: ImportAtomicity.PerRow,
    status: ImportProcessStatus.Succeeded,
    submittedByUserId: 'user-1',
    submittedByName: 'Dana Reyes',
    submittedOn: new Date('2026-09-11T23:38:00Z'),
    completedOn: new Date('2026-09-11T23:38:05Z'),
    totalRowCount: 10,
    succeededRowCount: 10,
    failedRowCount: 0,
    canManage: true,
    unappliedRowCount: 0,
    isTerminal: true,
    ...overrides,
  }
}

describe('buildImportRows', () => {
  it('leaves runs with no group as plain rows', () => {
    // Arrange
    const a = run()
    const b = run()

    // Act
    const rows = buildImportRows([a, b])

    // Assert
    expect(rows).toEqual([a, b])
  })

  it('folds the runs of one group under a rollup that keeps the newest run’s place', () => {
    // Arrange — newest first, as the list arrives; a lone run sits between the group's two
    const newest = run({ submissionGroupId: GROUP })
    const lone = run()
    const oldest = run({ submissionGroupId: GROUP })

    // Act
    const rows = buildImportRows([newest, lone, oldest])

    // Assert
    expect(rows.map((r) => r.id)).toEqual([`group:${GROUP}`, lone.id])
    expect(rows[0].submissionGroupId).toBe(GROUP)
    expect(rows[0].isGroup).toBe(true)
    expect(rows[0].children).toEqual([newest, oldest])
    expect(rows[0].displayName).toBe(groupLabel(2))
  })

  it('leaves a group of one as the run itself', () => {
    // Arrange — a rollup of one thing says nothing the thing does not
    const only = run({ submissionGroupId: GROUP })

    // Act
    const rows = buildImportRows([only])

    // Assert
    expect(rows).toEqual([only])
  })

  it('sums the counts and spans the timing', () => {
    // Arrange
    const first = run({
      submissionGroupId: GROUP,
      submittedOn: new Date('2026-09-11T23:38:00Z'),
      completedOn: new Date('2026-09-11T23:38:05Z'),
      totalRowCount: 28,
      succeededRowCount: 28,
    })
    const second = run({
      submissionGroupId: GROUP,
      submittedOn: new Date('2026-09-11T23:38:10Z'),
      completedOn: new Date('2026-09-11T23:38:40Z'),
      status: ImportProcessStatus.PartiallySucceeded,
      totalRowCount: 5,
      succeededRowCount: 3,
      failedRowCount: 1,
      unappliedRowCount: 1,
    })

    // Act
    const [rollup] = buildImportRows([second, first])

    // Assert
    expect(rollup.totalRowCount).toBe(33)
    expect(rollup.succeededRowCount).toBe(31)
    expect(rollup.failedRowCount).toBe(1)
    expect(rollup.unappliedRowCount).toBe(1)
    // Sorted by its newest file, so a batch never sinks behind runs posted mid-batch
    expect(rollup.submittedOn).toEqual(second.submittedOn)
    expect(rollup.completedOn).toEqual(second.completedOn)
    expect(rollup.submittedByName).toBe('Dana Reyes')
  })

  it('spans the timing when the dates arrive as strings, as the client delivers them', () => {
    // Arrange — the DTO says Date, the wire says ISO string, and the page gets the latter
    const asString = (value: string) => value as unknown as Date
    const first = run({
      submissionGroupId: GROUP,
      submittedOn: asString('2026-09-11T23:38:00Z'),
      startedOn: asString('2026-09-11T23:38:01Z'),
      completedOn: asString('2026-09-11T23:38:05Z'),
    })
    const second = run({
      submissionGroupId: GROUP,
      submittedOn: asString('2026-09-11T23:38:10Z'),
      startedOn: asString('2026-09-11T23:38:11Z'),
      completedOn: asString('2026-09-11T23:38:40Z'),
    })

    // Act
    const [rollup] = buildImportRows([second, first])

    // Assert
    expect(rollup.submittedOn).toBe(second.submittedOn)
    expect(rollup.startedOn).toBe(first.startedOn)
    expect(rollup.completedOn).toBe(second.completedOn)
  })

  it('offers no actions on the rollup, since each acts on one run', () => {
    // Arrange & Act
    const [rollup] = buildImportRows([
      run({ submissionGroupId: GROUP }),
      run({ submissionGroupId: GROUP }),
    ])

    // Assert
    expect(rollup.canManage).toBe(false)
  })

  describe('status', () => {
    const statusOf = (...statuses: ImportProcessStatus[]) =>
      buildImportRows(
        statuses.map((status) =>
          run({
            submissionGroupId: GROUP,
            status,
            succeededRowCount:
              status === ImportProcessStatus.Failed ||
              status === ImportProcessStatus.Cancelled
                ? 0
                : 10,
            isTerminal:
              status !== ImportProcessStatus.Queued &&
              status !== ImportProcessStatus.Processing &&
              status !== ImportProcessStatus.Cancelling,
          }),
        ),
      )[0]

    it('succeeds only when every run did', () => {
      // Arrange & Act & Assert
      expect(
        statusOf(ImportProcessStatus.Succeeded, ImportProcessStatus.Succeeded)
          .status,
      ).toBe(ImportProcessStatus.Succeeded)
    })

    it('is still going while any run is', () => {
      // Arrange & Act
      const rollup = statusOf(
        ImportProcessStatus.Succeeded,
        ImportProcessStatus.Queued,
      )

      // Assert — and has no finish until then
      expect(rollup.status).toBe(ImportProcessStatus.Queued)
      expect(rollup.isTerminal).toBe(false)
      expect(rollup.completedOn).toBeUndefined()
    })

    it('reads as partial when some rows landed and some run did not', () => {
      // Arrange & Act & Assert
      expect(
        statusOf(ImportProcessStatus.Succeeded, ImportProcessStatus.Failed)
          .status,
      ).toBe(ImportProcessStatus.PartiallySucceeded)
    })

    it('fails when nothing landed at all', () => {
      // Arrange & Act & Assert
      expect(
        statusOf(ImportProcessStatus.Failed, ImportProcessStatus.Failed).status,
      ).toBe(ImportProcessStatus.Failed)
    })
  })
})
