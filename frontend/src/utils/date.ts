export function formatShortDate(iso) {
  const d = new Date(iso)
  const now = new Date()
  if (d.toDateString() === now.toDateString()) {
    return d.toLocaleTimeString(['el-GR'], { hour: '2-digit', minute: '2-digit' })
  }
  if (d.getFullYear() === now.getFullYear()) {
    return d.toLocaleDateString(['el-GR'], { month: 'short', day: 'numeric' })
  }
  return d.toLocaleDateString()
}

export function formatLongDate(iso) {
  return new Date(iso).toLocaleString(['el-GR'], {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}
