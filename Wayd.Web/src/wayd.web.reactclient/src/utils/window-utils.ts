/**
 * Navigates with a full page load instead of a client-side route change.
 *
 * Sign-in, sign-out and setup need one: auth state and the API cache are
 * hydrated per page load, so a router push would carry the previous session's
 * state onto the next page. Code outside React (the API client, the OIDC
 * registry) has no router to push with.
 */
export const navigateWithFullReload = (path: string): void => {
  window.location.assign(path)
}

// a method to determine the drawer width based on the window size
// the drawer width is calculated based on the window size
export const getDrawerWidthPixels = (): number => {
  const width = window.innerWidth
  return width >= 1500
    ? Math.floor(width * 0.3)
    : width >= 1300
      ? Math.floor(width * 0.35)
      : width >= 1100
        ? Math.floor(width * 0.4)
        : width >= 900
          ? Math.floor(width * 0.5)
          : Math.floor(width * 0.8)
}
