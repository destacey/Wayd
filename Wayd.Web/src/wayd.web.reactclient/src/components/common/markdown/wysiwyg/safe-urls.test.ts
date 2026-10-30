import { isSafeImageSrc, isSafeLinkHref } from './safe-urls'

describe('isSafeLinkHref', () => {
  it.each([
    'https://wayd.dev',
    'http://wayd.dev/a?b=c',
    'mailto:someone@example.com',
    '/ppm/projects/12',
    '#section',
    'relative/page',
  ])('allows %s', (href) => {
    // Arrange / Act / Assert
    expect(isSafeLinkHref(href)).toBe(true)
  })

  it.each([
    'javascript:alert(1)',
    'JavaScript:alert(1)',
    ' javascript:alert(1)',
    'java\nscript:alert(1)',
    'java\tscript:alert(1)',
    'vbscript:msgbox(1)',
    'data:text/html,<script>alert(1)</script>',
    'file:///etc/passwd',
    '',
    undefined,
  ])('refuses %p', (href) => {
    // Arrange / Act / Assert
    expect(isSafeLinkHref(href)).toBe(false)
  })
})

describe('isSafeImageSrc', () => {
  it.each([
    ['https://wayd.dev/logo.png', true],
    ['http://wayd.dev/logo.png', true],
    ['data:image/png;base64,AAAA', false],
    ['file:///C:/image.png', false],
    ['javascript:alert(1)', false],
    ['/relative.png', false],
    [null, false],
  ])('%p is %p', (src, expected) => {
    // Arrange / Act / Assert
    expect(isSafeImageSrc(src)).toBe(expected)
  })
})
