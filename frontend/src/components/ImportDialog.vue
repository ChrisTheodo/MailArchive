<script setup lang="ts">
import { watch, type Ref, computed, ref, useTemplateRef } from 'vue'
import { useRoute } from 'vue-router'
import { useImportStore } from '@/stores/import'
import { useMailboxStore } from '@/stores/mailbox'

type MailboxMode = 'existing' | 'new'

// CONSTANTS
const importStore = useImportStore()
const mailboxStore = useMailboxStore()
const route = useRoute()
const MAX_SIZE = 1024 * 1024 * 1024

// STATE
const files = ref<File | File[] | null>(null)
const mailboxMode = ref<MailboxMode>('existing')
const mailboxId = ref<string | null>(null)
const newMailboxName = ref('')
const formRef = useTemplateRef('form')
const selectedFile = computed(() => (Array.isArray(files.value) ? files.value[0] : files.value))
const isSubmitting = computed(
  () =>
    mailboxStore.isCreatingMailbox ||
    mailboxStore.isDeletingMailbox ||
    importStore.isUploading ||
    importStore.isStartingProcessing,
)

// METHODS
function stripExtension(fileName: string) {
  return fileName.replace(/\.pst$/i, '')
}

function requiredRule(value: unknown) {
  if (Array.isArray(value) ? value.length : value) return true
  return 'Το πεδίο είναι υποχρεωτικό'
}

function maxFileSizeRule(value: File | File[] | null) {
  const list = Array.isArray(value) ? value : value ? [value] : []

  for (const file of list) {
    if (file.size > MAX_SIZE) {
      return `Το αρχείο ${file.name} ξεπερνάει το όριο των 1 GB`
    }
  }

  return true
}

function pstExtensionRule(value: File | File[] | null) {
  const list = Array.isArray(value) ? value : value ? [value] : []
  if (list.every((file) => file.name.toLowerCase().endsWith('.pst'))) return true
  return 'Επιτρέπονται μόνο αρχεία .pst'
}

function onOpen(isOpen: boolean) {
  if (!isOpen) return
  files.value = null
  newMailboxName.value = ''
  const routeMailbox = route.params.mailbox
  mailboxId.value = typeof routeMailbox === 'string' ? routeMailbox : null
  if (!mailboxStore.mailboxes.length) mailboxStore.fetchMailboxes()
  mailboxMode.value = mailboxId.value || mailboxStore.mailboxes.length ? 'existing' : 'new'
}

async function resolveMailboxId() {
  if (mailboxMode.value === 'existing') return mailboxId.value

  const mailbox = await mailboxStore.createMailbox({ displayName: newMailboxName.value.trim() })
  return mailbox?.id ?? null
}

async function confirmUpload(isActive: Ref<boolean>) {
  const result = await formRef.value?.validate()
  if (!result?.valid || !selectedFile.value) return

  const isNewMailbox = mailboxMode.value === 'new'
  const targetMailboxId = await resolveMailboxId()
  if (!targetMailboxId) return

  const batch = await importStore.importPstFile({
    mailboxId: targetMailboxId,
    file: selectedFile.value,
  })

  if (!batch) {
    if (isNewMailbox) {
      await mailboxStore.deleteMailbox({ id: targetMailboxId })
    }
    return
  }

  await importStore.processImportInBackground(batch.id)
  isActive.value = false

  importStore.fetchImports({ mailboxId: batch.mailboxId })
  mailboxStore.fetchMailboxes()
}

// WATCHERS
watch(selectedFile, (file, previous) => {
  const previousSuggestion = previous ? stripExtension(previous.name) : ''
  if (file && (!newMailboxName.value || newMailboxName.value === previousSuggestion)) {
    newMailboxName.value = stripExtension(file.name)
  }
})
</script>

<template>
  <v-dialog activator="parent" max-width="420" @update:model-value="onOpen">
    <template #default="{ isActive }">
      <v-card prepend-icon="i-mdi-import" title="Εισαγωγή αρχείου">
        <v-divider />
        <v-card-text>
          <v-form ref="form" @submit.prevent="confirmUpload(isActive)">
            <v-btn-toggle
              v-model="mailboxMode"
              mandatory
              divided
              density="compact"
              color="primary"
              variant="outlined"
              class="mb-4"
            >
              <v-btn
                value="existing"
                text="Υπάρχον mailbox"
                data-testid="existing-mailbox-button"
              />
              <v-btn value="new" text="Νέο mailbox" data-testid="new-mailbox-button" />
            </v-btn-toggle>

            <v-select
              v-if="mailboxMode === 'existing'"
              v-model="mailboxId"
              label="Mailbox"
              :items="mailboxStore.mailboxes"
              item-title="displayName"
              item-value="mailboxId"
              :loading="mailboxStore.isLoadingMailboxes"
              :rules="[requiredRule]"
              date-testid="mailbox-select"
            />
            <v-text-field
              v-else
              v-model="newMailboxName"
              label="Όνομα νέου mailbox"
              :rules="[requiredRule]"
              date-testid="new-mailbox-name-input"
            />

            <v-file-input
              v-model="files"
              label="Αρχείο .pst"
              accept=".pst"
              show-size
              :rules="[requiredRule, pstExtensionRule, maxFileSizeRule]"
              data-testid="pst-file-input"
            />
          </v-form>
        </v-card-text>

        <v-divider />

        <v-card-actions>
          <v-btn
            text="Κλείσιμο"
            :disabled="isSubmitting"
            @click="isActive.value = false"
            data-testid="close-button"
          />
          <v-btn
            text="Επιβεβαίωση"
            color="primary"
            variant="tonal"
            :loading="isSubmitting"
            @click="confirmUpload(isActive)"
            data-testid="submit-button"
          />
        </v-card-actions>
      </v-card>
    </template>
  </v-dialog>
</template>
