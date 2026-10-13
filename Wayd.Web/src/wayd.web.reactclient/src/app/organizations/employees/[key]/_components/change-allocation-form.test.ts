import dayjs from 'dayjs'
import { changesFor } from './change-allocation-form'

jest.unmock('dayjs')

const existing = {
  teamId: 'team-1',
  teamName: 'Cards',
  roleId: 'role-1',
  roleName: 'Engineer',
  start: '2027-01-01',
  end: undefined,
  percent: 50,
}

describe('changesFor', () => {
  it('is a single change when allocating afresh', () => {
    const changes = changesFor(undefined, {
      teamId: 'team-2',
      roleId: undefined,
      start: dayjs('2027-02-01'),
      end: dayjs('2027-02-07'),
      percent: 40,
    })

    expect(changes).toEqual([
      {
        teamId: 'team-2',
        roleId: undefined,
        start: '2027-02-01',
        end: '2027-02-07',
        percent: 40,
      },
    ])
  })

  it('clears the original range before setting the new one, so a later start takes effect', () => {
    const changes = changesFor(existing, {
      teamId: 'team-1',
      roleId: 'role-1',
      start: dayjs('2027-03-01'),
      end: null,
      percent: 50,
    })

    expect(changes).toEqual([
      {
        teamId: 'team-1',
        roleId: 'role-1',
        start: '2027-01-01',
        end: undefined,
        percent: 0,
      },
      {
        teamId: 'team-1',
        roleId: 'role-1',
        start: '2027-03-01',
        end: undefined,
        percent: 50,
      },
    ])
  })

  it('clears the original range so an earlier end takes effect', () => {
    const changes = changesFor(
      { ...existing, end: '2027-12-31' },
      {
        teamId: 'team-1',
        roleId: 'role-1',
        start: dayjs('2027-01-01'),
        end: dayjs('2027-06-30'),
        percent: 50,
      },
    )

    expect(changes[0]).toMatchObject({
      start: '2027-01-01',
      end: '2027-12-31',
      percent: 0,
    })
    expect(changes[1]).toMatchObject({
      start: '2027-01-01',
      end: '2027-06-30',
      percent: 50,
    })
  })

  it("keeps the allocation's own team and role whatever the form holds", () => {
    const changes = changesFor(existing, {
      teamId: 'team-9',
      roleId: 'role-9',
      start: dayjs('2027-01-01'),
      end: null,
      percent: 80,
    })

    expect(changes.every((c) => c.teamId === 'team-1')).toBe(true)
    expect(changes.every((c) => c.roleId === 'role-1')).toBe(true)
  })
})
