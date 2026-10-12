<script setup lang="ts">
import { type Ref, ref, useTemplateRef } from 'vue'
import { useImportStore } from '@/stores/import'

const { importPstFile } = useImportStore()

// STATE
const files = ref<File[]>([])
const fileInputRef = useTemplateRef('fileInput')

// METHODS
function maxFileSizeRule(files: File[]) {
  if (!files || files.length === 0) return true
  const MAX_SIZE = 1024 * 1024 * 1024

  for (const file of files) {
    if (file.size > MAX_SIZE) {
      return `Το αρχείο ${file.name} ξεπερνάει το όριο των 1 GB`
    }
  }

  return true
}

async function confirmUpload(isActive: Ref<boolean>) {
  if (!fileInputRef.value?.isValid) return

  await importPstFile()
  isActive.value = false
}
</script>

<template>
  <v-dialog activator="parent" max-width="420">
    <template #default="{ isActive }">
      <v-card prepend-icon="i-mdi-import" title="Εισαγωγή αρχείου">
        <v-card-text>
          <v-file-input
            ref="fileInput"
            v-model="files"
            label="Αρχείο .pst"
            accept=".pst"
            show-size
            :rules="[maxFileSizeRule]"
          />
        </v-card-text>

        <v-divider />

        <v-card-actions>
          <v-btn text="Κλείσιμο" @click="isActive.value = false" />
          <v-btn
            text="Επιβεβαίωση"
            color="primary"
            variant="tonal"
            @click="confirmUpload(isActive)"
          />
        </v-card-actions>
      </v-card>
    </template>
  </v-dialog>
</template>
