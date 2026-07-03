import type { ColumnDef } from '@tanstack/react-table'
import { applySafeAccessor } from './column-accessors'

type Item = {
  name: string
  team?: { name: string } | null
}

const getAccessorFn = <T>(col: ColumnDef<T, unknown>) =>
  (col as { accessorFn?: (row: T, index: number) => unknown }).accessorFn

describe('applySafeAccessor', () => {
  it('returns non-dotted defs unchanged (same reference)', () => {
    // Arrange
    const col: ColumnDef<Item, any> = { accessorKey: 'name', header: 'Name' }

    // Act
    const result = applySafeAccessor(col)

    // Assert
    expect(result).toBe(col)
  })

  it('replaces a dotted accessorKey with an accessorFn resolving the path', () => {
    // Arrange
    const col: ColumnDef<Item, any> = {
      accessorKey: 'team.name',
      header: 'Team',
    }

    // Act
    const result = applySafeAccessor(col)

    // Assert
    expect((result as { accessorKey?: string }).accessorKey).toBeUndefined()
    const accessorFn = getAccessorFn(result)!
    expect(accessorFn({ name: 'a', team: { name: 'Juice' } }, 0)).toBe('Juice')
  })

  it('returns undefined (without throwing) when an intermediate hop is missing', () => {
    // Arrange
    const col: ColumnDef<Item, any> = {
      accessorKey: 'team.name',
      header: 'Team',
    }

    // Act
    const accessorFn = getAccessorFn(applySafeAccessor(col))!

    // Assert
    expect(accessorFn({ name: 'a' }, 0)).toBeUndefined()
    expect(accessorFn({ name: 'a', team: null }, 0)).toBeUndefined()
  })

  it('derives the column id the way TanStack does (dots to underscores)', () => {
    // Arrange
    const col: ColumnDef<Item, any> = {
      accessorKey: 'team.name',
      header: 'Team',
    }

    // Act
    const result = applySafeAccessor(col)

    // Assert
    expect(result.id).toBe('team_name')
  })

  it('keeps an explicit column id', () => {
    // Arrange
    const col: ColumnDef<Item, any> = {
      id: 'team',
      accessorKey: 'team.name',
      header: 'Team',
    }

    // Act
    const result = applySafeAccessor(col)

    // Assert
    expect(result.id).toBe('team')
  })

  it('recurses into grouped-header defs', () => {
    // Arrange
    const group: ColumnDef<Item, any> = {
      id: 'info',
      header: 'Info',
      columns: [
        { accessorKey: 'name', header: 'Name' },
        { accessorKey: 'team.name', header: 'Team' },
      ],
    }

    // Act
    const result = applySafeAccessor(group) as {
      columns: ColumnDef<Item, unknown>[]
    }

    // Assert
    const teamLeaf = result.columns[1]
    expect(getAccessorFn(teamLeaf)).toBeDefined()
    expect(getAccessorFn(teamLeaf)!({ name: 'a', team: { name: 'J' } }, 0)).toBe(
      'J',
    )
  })
})
