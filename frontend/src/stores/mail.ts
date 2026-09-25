import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

// Seed dates relative to "now" so the demo always looks fresh
const hoursAgo = h => new Date(Date.now() - h * 3_600_000).toISOString()

export const FOLDERS = [
  { key: 'inbox', title: 'Inbox', icon: 'mdi-inbox' },
  { key: 'starred', title: 'Starred', icon: 'mdi-star-outline' },
  { key: 'sent', title: 'Sent', icon: 'mdi-send-outline' },
  { key: 'drafts', title: 'Drafts', icon: 'mdi-file-document-outline' },
  { key: 'spam', title: 'Spam', icon: 'mdi-alert-octagon-outline' },
  { key: 'trash', title: 'Trash', icon: 'mdi-delete-outline' },
]

export const useMailStore = defineStore('mail', () => {
  // ---------- state ----------
  const labels = ref([
    { key: 'work', title: 'Work', color: 'blue' },
    { key: 'personal', title: 'Personal', color: 'green' },
    { key: 'travel', title: 'Travel', color: 'orange' },
  ])

  const search = ref('')

  const emails = ref([
    {
      id: 1, folder: 'inbox', labels: ['work'], read: false, starred: true,
      from: { name: 'Sara Ahmed', email: 'sara@company.com' },
      to: 'me@example.com',
      subject: 'Q4 planning meeting',
      body: 'Hi,\n\nCan we move the Q4 planning meeting to Thursday at 10am? I have a conflict on Wednesday.\n\nThanks,\nSara',
      date: hoursAgo(1),
    },
    {
      id: 2, folder: 'inbox', labels: ['travel'], read: false, starred: false,
      from: { name: 'SkyAir', email: 'no-reply@skyair.example' },
      to: 'me@example.com',
      subject: 'Your booking is confirmed',
      body: 'Your flight to Lisbon is confirmed.\n\nBooking reference: XK42LP\nDeparture: 14 Oct, 08:35\n\nHave a great trip!',
      date: hoursAgo(5),
    },
    {
      id: 3, folder: 'inbox', labels: ['personal'], read: true, starred: false,
      from: { name: 'Mom', email: 'mom@family.example' },
      to: 'me@example.com',
      subject: 'Dinner on Sunday?',
      body: 'Are you coming for dinner on Sunday? Let me know so I can plan.\n\nLove,\nMom',
      date: hoursAgo(26),
    },
    {
      id: 4, folder: 'inbox', labels: ['work'], read: true, starred: false,
      from: { name: 'GitHub', email: 'notifications@github.example' },
      to: 'me@example.com',
      subject: '[mail-app] PR #128 was merged',
      body: 'Your pull request "Add sidebar labels" was merged into main.',
      date: hoursAgo(50),
    },
    {
      id: 5, folder: 'inbox', labels: [], read: true, starred: true,
      from: { name: 'Newsletter Weekly', email: 'hello@newsletter.example' },
      to: 'me@example.com',
      subject: 'This week in web development',
      body: 'Top stories this week: Vue performance tips, Vuetify layout patterns, and more.',
      date: hoursAgo(120),
    },
    {
      id: 6, folder: 'sent', labels: ['work'], read: true, starred: false,
      from: { name: 'Me', email: 'me@example.com' },
      to: 'sara@company.com',
      subject: 'Re: Q4 planning meeting',
      body: 'Thursday at 10 works for me. See you then!',
      date: hoursAgo(0.5),
    },
    {
      id: 7, folder: 'drafts', labels: [], read: true, starred: false,
      from: { name: 'Me', email: 'me@example.com' },
      to: 'team@company.com',
      subject: 'Release notes draft',
      body: 'Draft: highlights for the upcoming release…',
      date: hoursAgo(30),
    },
    {
      id: 8, folder: 'spam', labels: [], read: false, starred: false,
      from: { name: 'Prize Center', email: 'win@totally-legit.example' },
      to: 'me@example.com',
      subject: 'You have WON!!!',
      body: 'Click here to claim your prize.',
      date: hoursAgo(200),
    },
  ])

  // ---------- getters ----------
  function matchesSearch (mail) {
    const q = search.value.trim().toLowerCase()
    if (!q) return true
    return [mail.subject, mail.body, mail.from.name, mail.from.email]
      .some(s => s.toLowerCase().includes(q))
  }

  const sortByDate = list => [...list].sort((a, b) => new Date(b.date) - new Date(a.date))

  function emailsInFolder (folder) {
    const list = folder === 'starred'
      ? emails.value.filter(m => m.starred && m.folder !== 'trash')
      : emails.value.filter(m => m.folder === folder)
    return sortByDate(list.filter(matchesSearch))
  }

  function emailsWithLabel (label) {
    return sortByDate(
      emails.value.filter(m => m.labels.includes(label) && m.folder !== 'trash' && matchesSearch(m)),
    )
  }

  function unreadCount (folder) {
    return emails.value.filter(m => m.folder === folder && !m.read).length
  }

  const getById = id => emails.value.find(m => m.id === Number(id))
  const getLabel = key => labels.value.find(l => l.key === key)
  const inboxUnread = computed(() => unreadCount('inbox'))

  // ---------- actions ----------
  function markRead (id, read = true) {
    const mail = getById(id)
    if (mail) mail.read = read
  }

  function toggleStar (id) {
    const mail = getById(id)
    if (mail) mail.starred = !mail.starred
  }

  function moveTo (id, folder) {
    const mail = getById(id)
    if (mail) mail.folder = folder
  }

  function deleteMail (id) {
    const mail = getById(id)
    if (!mail) return
    if (mail.folder === 'trash') {
      emails.value = emails.value.filter(m => m.id !== mail.id) // permanent delete
    } else {
      mail.folder = 'trash'
    }
  }

  // async function fetchEmails () {
  //   const res = await fetch('/api/emails')
  //   emails.value = await res.json()
  // }

  return {
    labels, emails, search,
    emailsInFolder, emailsWithLabel, unreadCount, getById, getLabel, inboxUnread,
    markRead, toggleStar, moveTo, deleteMail,
  }
})
