<script setup lang="ts">
import { useImportStore } from '@/stores/import'
import ConfirmDialog from './ConfirmDialog.vue'

const props = defineProps<{
  importId: string
  importName?: string
  statusLabel?: string
}>()

const emit = defineEmits<{ cancelled: [] }>()

const isOpen = defineModel<boolean>({ default: false })

// CONSTANTS
const importStore = useImportStore()

// METHODS
async function onConfirm() {
  const ok = await importStore.cancelImport({ id: props.importId })
  if (!ok) return

  isOpen.value = false
  emit('cancelled')
}
</script>

<template>
  <ConfirmDialog
    v-model="isOpen"
    title="Ακύρωση εισαγωγής αρχείου"
    icon="i-mdi-upload-off"
    confirm-color="error"
    confirm-text="Ακύρωση εισαγωγής"
    cancel-text="Πίσω"
    :loading="importStore.isCancelling"
    @confirm="onConfirm"
  >
    Είστε σίγουροι ότι θέλετε να ακυρώσετε την εισαγωγή
    <strong v-if="importName">{{ importName }}</strong>
    <template v-if="statusLabel">
      με κατάσταση <strong>{{ statusLabel }}</strong></template
    >;
  </ConfirmDialog>
</template>
