export function getScrollbarWidth(el: HTMLElement): number {
  const style = getComputedStyle(el)
  const borders = parseFloat(style.borderLeftWidth) + parseFloat(style.borderRightWidth)
  return el.offsetWidth - el.clientWidth - borders
}
