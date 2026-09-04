import {
  getPlanScheduleLabel,
  isPlanItemOverdue,
} from './project-plan-schedule'

describe('isPlanItemOverdue', () => {
  const now = new Date(2026, 8, 4)
  const inProgress: any = { name: 'In Progress' }

  it('returns true when the end date is before today', () => {
    // Arrange
    const node: any = { status: inProgress, end: new Date(2026, 8, 3) }

    // Act
    const result = isPlanItemOverdue(node, now)

    // Assert
    expect(result).toBe(true)
  })

  it('returns false when the end date is today', () => {
    // Arrange
    const node: any = { status: inProgress, end: new Date(2026, 8, 4) }

    // Act
    const result = isPlanItemOverdue(node, now)

    // Assert
    expect(result).toBe(false)
  })

  it('returns false when the end date is in the future', () => {
    // Arrange
    const node: any = { status: inProgress, end: new Date(2026, 8, 5) }

    // Act
    const result = isPlanItemOverdue(node, now)

    // Assert
    expect(result).toBe(false)
  })

  it('returns false for a completed item with a past end date', () => {
    // Arrange
    const node: any = {
      status: { name: 'Completed' },
      end: new Date(2026, 8, 3),
    }

    // Act
    const result = isPlanItemOverdue(node, now)

    // Assert
    expect(result).toBe(false)
  })

  it('returns false for a canceled item with a past end date', () => {
    // Arrange
    const node: any = {
      status: { name: 'Canceled' },
      end: new Date(2026, 8, 3),
    }

    // Act
    const result = isPlanItemOverdue(node, now)

    // Assert
    expect(result).toBe(false)
  })

  it('falls back to plannedDate when there is no end date', () => {
    // Arrange
    const node: any = {
      status: inProgress,
      plannedDate: new Date(2026, 8, 1),
    }

    // Act
    const result = isPlanItemOverdue(node, now)

    // Assert
    expect(result).toBe(true)
  })

  it('returns false when the item has no dates', () => {
    // Arrange
    const node: any = { status: inProgress }

    // Act
    const result = isPlanItemOverdue(node, now)

    // Assert
    expect(result).toBe(false)
  })

  it('ignores the time of day on a past end date', () => {
    // Arrange
    const node: any = {
      status: inProgress,
      end: new Date(2026, 8, 4, 23, 59),
    }

    // Act
    const result = isPlanItemOverdue(node, new Date(2026, 8, 4, 0, 1))

    // Assert
    expect(result).toBe(false)
  })
})

describe('isPlanItemOverdue with API date strings', () => {
  const inProgress: any = { name: 'In Progress' }

  // Plan dates arrive as bare NodaTime LocalDate strings ("2026-09-04").
  it('does not treat a task due today as overdue', () => {
    // Arrange
    const node: any = { status: inProgress, end: '2026-09-04' }

    // Act
    const result = isPlanItemOverdue(node, new Date(2026, 8, 4, 9, 30))

    // Assert
    expect(result).toBe(false)
  })

  it('treats a task due yesterday as overdue', () => {
    // Arrange
    const node: any = { status: inProgress, end: '2026-09-03' }

    // Act
    const result = isPlanItemOverdue(node, new Date(2026, 8, 4, 9, 30))

    // Assert
    expect(result).toBe(true)
  })

  it('does not treat a task due tomorrow as overdue', () => {
    // Arrange
    const node: any = { status: inProgress, end: '2026-09-05' }

    // Act
    const result = isPlanItemOverdue(node, new Date(2026, 8, 4, 9, 30))

    // Assert
    expect(result).toBe(false)
  })

  it('does not treat a milestone due today as overdue', () => {
    // Arrange
    const node: any = { status: inProgress, plannedDate: '2026-09-04' }

    // Act
    const result = isPlanItemOverdue(node, new Date(2026, 8, 4, 9, 30))

    // Assert
    expect(result).toBe(false)
  })

  it('handles a full timestamp string without shifting the day', () => {
    // Arrange
    const node: any = { status: inProgress, end: '2026-09-04T00:00:00Z' }

    // Act
    const result = isPlanItemOverdue(node, new Date(2026, 8, 4, 9, 30))

    // Assert
    expect(result).toBe(false)
  })

  it('returns false for an unparseable date', () => {
    // Arrange
    const node: any = { status: inProgress, end: 'not a date' }

    // Act
    const result = isPlanItemOverdue(node, new Date(2026, 8, 4))

    // Assert
    expect(result).toBe(false)
  })
})

describe('getPlanScheduleLabel', () => {
  const inProgress: any = { name: 'In Progress' }

  // Wednesday 2026-09-02. That week's Saturday is the 5th, so "this week"
  // runs through the 5th and "upcoming" through the 12th.
  const wednesday = new Date(2026, 8, 2, 10, 0)

  it('labels a past date Overdue', () => {
    // Arrange
    const node: any = { status: inProgress, end: '2026-09-01' }

    // Act
    const result = getPlanScheduleLabel(node, wednesday)

    // Assert
    expect(result).toBe('Overdue')
  })

  it('labels today Due This Week rather than a bucket of its own', () => {
    // Arrange
    const node: any = { status: inProgress, end: '2026-09-02' }

    // Act
    const result = getPlanScheduleLabel(node, wednesday)

    // Assert
    expect(result).toBe('Due This Week')
  })

  it('includes the closing Saturday in Due This Week', () => {
    // Arrange
    const node: any = { status: inProgress, end: '2026-09-05' }

    // Act
    const result = getPlanScheduleLabel(node, wednesday)

    // Assert
    expect(result).toBe('Due This Week')
  })

  it('labels the day after Saturday Upcoming', () => {
    // Arrange
    const node: any = { status: inProgress, end: '2026-09-06' }

    // Act
    const result = getPlanScheduleLabel(node, wednesday)

    // Assert
    expect(result).toBe('Upcoming')
  })

  it('includes the following Saturday in Upcoming', () => {
    // Arrange
    const node: any = { status: inProgress, end: '2026-09-12' }

    // Act
    const result = getPlanScheduleLabel(node, wednesday)

    // Assert
    expect(result).toBe('Upcoming')
  })

  it('leaves anything beyond next Saturday unlabelled', () => {
    // Arrange
    const node: any = { status: inProgress, end: '2026-09-13' }

    // Act
    const result = getPlanScheduleLabel(node, wednesday)

    // Assert
    expect(result).toBeNull()
  })

  it('leaves a completed task unlabelled even when overdue', () => {
    // Arrange
    const node: any = { status: { name: 'Completed' }, end: '2026-08-01' }

    // Act
    const result = getPlanScheduleLabel(node, wednesday)

    // Assert
    expect(result).toBeNull()
  })

  it('leaves a canceled task unlabelled', () => {
    // Arrange
    const node: any = { status: { name: 'Canceled' }, end: '2026-08-01' }

    // Act
    const result = getPlanScheduleLabel(node, wednesday)

    // Assert
    expect(result).toBeNull()
  })

  it('leaves an undated task unlabelled', () => {
    // Arrange
    const node: any = { status: inProgress }

    // Act
    const result = getPlanScheduleLabel(node, wednesday)

    // Assert
    expect(result).toBeNull()
  })

  it('uses a milestone plannedDate when there is no end date', () => {
    // Arrange
    const node: any = { status: inProgress, plannedDate: '2026-09-03' }

    // Act
    const result = getPlanScheduleLabel(node, wednesday)

    // Assert
    expect(result).toBe('Due This Week')
  })

  it('treats Saturday itself as a full week, not an empty one', () => {
    // Arrange
    const saturday = new Date(2026, 8, 5, 10, 0)
    const node: any = { status: inProgress, end: '2026-09-05' }

    // Act
    const result = getPlanScheduleLabel(node, saturday)

    // Assert
    expect(result).toBe('Due This Week')
  })

  it('rolls the week over on Sunday', () => {
    // Arrange
    const sunday = new Date(2026, 8, 6, 10, 0)
    const node: any = { status: inProgress, end: '2026-09-12' }

    // Act
    const result = getPlanScheduleLabel(node, sunday)

    // Assert
    expect(result).toBe('Due This Week')
  })

  it('crosses a month boundary without shifting the week', () => {
    // Arrange
    const tuesday = new Date(2026, 8, 29, 10, 0)
    const node: any = { status: inProgress, end: '2026-10-03' }

    // Act
    const result = getPlanScheduleLabel(node, tuesday)

    // Assert
    expect(result).toBe('Due This Week')
  })
})

describe('schedule buckets shared with the dashboard badges', () => {
  const inProgress: any = { name: 'In Progress' }

  // The dashboard's TaskRow badge and its per-stage counts read these same
  // buckets, so a task due today must not land in a bucket of its own — it was
  // previously labelled "Due Today" there and counted as due this week, while
  // a task due later in the same week fell through to Upcoming.
  const wednesday = new Date(2026, 8, 2, 10, 0)

  it('puts today and later-this-week in the same bucket', () => {
    // Arrange
    const dueToday: any = { status: inProgress, end: '2026-09-02' }
    const dueThursday: any = { status: inProgress, end: '2026-09-03' }

    // Act
    const todayLabel = getPlanScheduleLabel(dueToday, wednesday)
    const thursdayLabel = getPlanScheduleLabel(dueThursday, wednesday)

    // Assert
    expect(todayLabel).toBe('Due This Week')
    expect(thursdayLabel).toBe('Due This Week')
  })

  it('never returns a Due Today bucket', () => {
    // Arrange
    const days = ['2026-09-01', '2026-09-02', '2026-09-05', '2026-09-12']

    // Act
    const labels = days.map((end) =>
      getPlanScheduleLabel({ status: inProgress, end } as any, wednesday),
    )

    // Assert
    expect(labels).not.toContain('Due Today')
  })
})
