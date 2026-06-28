// timeline2/render/suppress-next-click.ts
// After a drag, the browser still fires a synthetic `click` on pointer-up.
// Call this on drag end to swallow exactly that one click (capture phase) so it
// doesn't trigger the item's onClick (e.g. opening a drawer).

export function suppressNextClick() {
  const onClickCapture = (ev: MouseEvent) => {
    ev.stopPropagation()
    ev.preventDefault()
    window.removeEventListener('click', onClickCapture, true)
  }
  window.addEventListener('click', onClickCapture, true)
  // Safety: if no click arrives (pointer released off-target), drop the listener.
  setTimeout(() => window.removeEventListener('click', onClickCapture, true), 0)
}
