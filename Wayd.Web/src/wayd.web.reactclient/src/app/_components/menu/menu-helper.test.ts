import {
  buildRouteKeyMap,
  findMenuKeysByPathname,
  Item,
  menuItem,
} from './menu-helper'

const testMenuItems: Item[] = [
  menuItem('Home', 'home', '/'),
  menuItem('Organizations', 'org', undefined, undefined, [
    menuItem('Teams', 'org.teams', '/organizations/teams'),
    menuItem('Employees', 'org.employees', '/organizations/employees'),
  ]),
  menuItem('PPM', 'ppm', undefined, undefined, [
    menuItem('Projects Dashboard', 'ppm.dashboards.projects', '/ppm/dashboards/projects'),
    menuItem('Portfolios', 'ppm.portfolios', '/ppm/portfolios'),
    menuItem('Projects', 'ppm.projects', '/ppm/projects'),
  ]),
  menuItem('Settings', 'settings', '/settings'),
]

describe('buildRouteKeyMap', () => {
  it('should map routes to their menu keys', () => {
    const map = buildRouteKeyMap(testMenuItems)

    expect(map.get('/')).toBe('home')
    expect(map.get('/organizations/teams')).toBe('org.teams')
    expect(map.get('/ppm/projects')).toBe('ppm.projects')
    expect(map.get('/settings')).toBe('settings')
  })

  it('should include nested children', () => {
    const map = buildRouteKeyMap(testMenuItems)

    expect(map.get('/ppm/dashboards/projects')).toBe('ppm.dashboards.projects')
    expect(map.get('/organizations/employees')).toBe('org.employees')
  })

  it('should skip items without routes', () => {
    const map = buildRouteKeyMap(testMenuItems)

    // Parent items 'org' and 'ppm' have no route
    expect(map.has('org')).toBe(false)
    expect(map.has('ppm')).toBe(false)
  })
})

describe('findMenuKeysByPathname', () => {
  const routeKeyMap = buildRouteKeyMap(testMenuItems)

  it('should match exact route', () => {
    const result = findMenuKeysByPathname('/ppm/projects', routeKeyMap)

    expect(result.selectedKeys).toEqual(['ppm.projects'])
    expect(result.openKeys).toEqual(['ppm'])
  })

  it('should match home route exactly', () => {
    const result = findMenuKeysByPathname('/', routeKeyMap)

    expect(result.selectedKeys).toEqual(['home'])
    expect(result.openKeys).toEqual([])
  })

  it('should not match home route as prefix for other paths', () => {
    const result = findMenuKeysByPathname('/organizations/teams', routeKeyMap)

    expect(result.selectedKeys).toEqual(['org.teams'])
    // Should not include 'home'
    expect(result.openKeys).toEqual(['org'])
  })

  it('should match detail pages to their parent route via prefix', () => {
    const result = findMenuKeysByPathname('/ppm/projects/NETMIGRATION', routeKeyMap)

    expect(result.selectedKeys).toEqual(['ppm.projects'])
    expect(result.openKeys).toEqual(['ppm'])
  })

  it('should use longest prefix match', () => {
    // /ppm/dashboards/projects is more specific than /ppm
    const result = findMenuKeysByPathname('/ppm/dashboards/projects/some-detail', routeKeyMap)

    expect(result.selectedKeys).toEqual(['ppm.dashboards.projects'])
    expect(result.openKeys).toEqual(['ppm'])
  })

  it('should match by shared segments when no exact prefix match exists', () => {
    // /organizations/team-of-teams/5 shares the /organizations segment
    // with org.teams, so it selects Teams and opens the org submenu
    const result = findMenuKeysByPathname('/organizations/team-of-teams/5', routeKeyMap)

    expect(result.selectedKeys).toEqual(['org.teams'])
    expect(result.openKeys).toEqual(['org'])
  })

  it('should return empty arrays for completely unknown pathname', () => {
    const result = findMenuKeysByPathname('/unknown/page', routeKeyMap)

    expect(result.selectedKeys).toEqual([])
    expect(result.openKeys).toEqual([])
  })

  it('should not include openKeys for top-level items', () => {
    const result = findMenuKeysByPathname('/settings', routeKeyMap)

    expect(result.selectedKeys).toEqual(['settings'])
    expect(result.openKeys).toEqual([])
  })

  it('should match settings sub-pages to settings', () => {
    const result = findMenuKeysByPathname('/settings/user-management/users', routeKeyMap)

    expect(result.selectedKeys).toEqual(['settings'])
    expect(result.openKeys).toEqual([])
  })
})
