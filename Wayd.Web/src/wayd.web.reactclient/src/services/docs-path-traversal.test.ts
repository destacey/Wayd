import fs from 'fs'
import path from 'path'
import { resolveDocPath, getDocBySlug } from './docs'

const DOCS_ROOT = process.env.DOCS_PATH
  ? path.resolve(process.env.DOCS_PATH)
  : path.join(process.cwd(), '..', '..', '..', 'docs')

// Files that exist OUTSIDE the docs root and end in .md, so the forced extension does not
// save us. If these ever stop existing the traversal tests would pass vacuously, so the
// first test asserts they are really there.
const OUTSIDE_TARGETS = ['README', 'CLAUDE', 'AGENTS']

describe('resolveDocPath containment', () => {
  it('the traversal targets used below really exist outside the docs root', () => {
    // Arrange
    const repoRoot = path.resolve(DOCS_ROOT, '..')

    // Act
    const present = OUTSIDE_TARGETS.filter((name) =>
      fs.existsSync(path.join(repoRoot, `${name}.md`)),
    )

    // Assert
    // Guards against the traversal assertions passing simply because there is nothing to reach.
    expect(present.length).toBeGreaterThan(0)
  })

  describe('rejects slugs that escape the docs root', () => {
    const hostileSlugs: [string, string[]][] = [
      ['parent-relative file', ['../README']],
      ['backslash separator', ['..\\README']],
      ['discrete dot-dot segments', ['..', '..', 'package']],
      ['two levels up', ['../../README']],
      ['traversal after a real segment', ['docs', '..', '..', 'README']],
      ['re-entrant traversal', ['contributing', '../../README']],
      ['embedded separator', ['a/../../README']],
      ['bare dot-dot', ['..']],
      ['empty segment', ['']],
      ['empty segment among real ones', ['contributing', '', 'index']],
      ['decoded separator only', ['contributing/index']],
      ['absolute-looking segment', ['/etc/passwd']],
    ]

    it.each(hostileSlugs)('%s', (_label, slug) => {
      // Arrange
      const readSpy = jest.spyOn(fs, 'readFileSync')

      // Act
      const resolved = resolveDocPath(slug)
      const doc = getDocBySlug(slug)

      // Assert
      expect(resolved).toBeNull()
      expect(doc).toBeNull()
      expect(readSpy).not.toHaveBeenCalled()

      readSpy.mockRestore()
    })
  })

  it('never returns a path outside the docs root for any hostile depth', () => {
    // Arrange
    const root = path.resolve(DOCS_ROOT)
    const depths = [1, 2, 3, 4, 5]

    // Act
    const escapes = depths
      .flatMap((depth) =>
        OUTSIDE_TARGETS.map((name) => [
          ...Array<string>(depth).fill('..'),
          name,
        ]),
      )
      .map((slug) => resolveDocPath(slug))
      .filter(
        (resolved): resolved is string =>
          resolved !== null && !resolved.startsWith(root + path.sep),
      )

    // Assert
    expect(escapes).toEqual([])
  })

  it('rejects a sibling directory that shares the docs root prefix', () => {
    // Arrange
    // `docs-site` would satisfy a naive startsWith(DOCS_ROOT) that omits the trailing
    // separator. Reaching it requires stepping out and back in.
    const slug = ['..', 'docs-site', 'index']

    // Act
    const resolved = resolveDocPath(slug)

    // Assert
    expect(resolved).toBeNull()
  })
})

describe('resolveDocPath still resolves legitimate docs', () => {
  it('resolves the root index', () => {
    // Arrange
    const slug = ['index']

    // Act
    const resolved = resolveDocPath(slug)

    // Assert
    expect(resolved).not.toBeNull()
    expect(resolved).toBe(path.join(DOCS_ROOT, 'index.mdx'))
  })

  it.each([
    ['getting-started'],
    ['contributing'],
    ['user-guide'],
    ['reference'],
  ])(
    'resolves the directory-index form page.tsx relies on: %s',
    (section) => {
      // Arrange
      // page.tsx loads a directory page as getDocBySlug([...slug, 'index']).
      const slug = [section, 'index']

      // Act
      const resolved = resolveDocPath(slug)

      // Assert
      expect(resolved).not.toBeNull()
      expect(resolved!.startsWith(path.resolve(DOCS_ROOT) + path.sep)).toBe(true)
    },
  )

  it('resolves a nested leaf page', () => {
    // Arrange
    const slug = ['ai', 'domain-glossary']

    // Act
    const doc = getDocBySlug(slug)

    // Assert
    expect(doc).not.toBeNull()
    expect(doc!.content.length).toBeGreaterThan(0)
  })

  it('resolves a nested directory index', () => {
    // Arrange
    const slug = ['user-guide', 'ppm', 'index']

    // Act
    const resolved = resolveDocPath(slug)

    // Assert
    expect(resolved).not.toBeNull()
    expect(resolved!.startsWith(path.resolve(DOCS_ROOT) + path.sep)).toBe(true)
  })

  it('returns null for a slug that is simply absent, without throwing', () => {
    // Arrange
    const slug = ['no-such-page-anywhere']

    // Act
    const resolved = resolveDocPath(slug)

    // Assert
    expect(resolved).toBeNull()
  })
})
