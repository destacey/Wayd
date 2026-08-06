import { renderHook } from '@testing-library/react'
import { useLinkedEmployee } from './use-linked-employee'
import useAuth from '../components/contexts/auth'

jest.mock('../components/contexts/auth', () => ({
  __esModule: true,
  default: jest.fn(),
}))

const mockUseAuth = useAuth as jest.Mock

describe('useLinkedEmployee', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('reports a linked employee when the user has an employee id', () => {
    mockUseAuth.mockReturnValue({
      user: { employeeId: 'e1b7c3a2-0000-4000-8000-000000000001' },
    })

    const { result } = renderHook(() => useLinkedEmployee())

    expect(result.current.hasLinkedEmployee).toBe(true)
    expect(result.current.employeeId).toBe(
      'e1b7c3a2-0000-4000-8000-000000000001',
    )
  })

  it('reports no linked employee when the employee id is null', () => {
    mockUseAuth.mockReturnValue({ user: { employeeId: null } })

    const { result } = renderHook(() => useLinkedEmployee())

    expect(result.current.hasLinkedEmployee).toBe(false)
    expect(result.current.employeeId).toBeNull()
  })

  it('reports no linked employee when the employee id is undefined', () => {
    mockUseAuth.mockReturnValue({ user: { employeeId: undefined } })

    const { result } = renderHook(() => useLinkedEmployee())

    expect(result.current.hasLinkedEmployee).toBe(false)
    expect(result.current.employeeId).toBeNull()
  })

  it('reports no linked employee when there is no user', () => {
    mockUseAuth.mockReturnValue({ user: null })

    const { result } = renderHook(() => useLinkedEmployee())

    expect(result.current.hasLinkedEmployee).toBe(false)
    expect(result.current.employeeId).toBeNull()
  })
})
