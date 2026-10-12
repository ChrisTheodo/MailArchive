<script setup lang="ts">
interface Props {
  title?: string
  message?: string
  confirmText?: string
  cancelText?: string
  confirmColor?: string
  icon?: string
  loading?: boolean
  persistent?: boolean
  maxWidth?: string | number
}

withDefaults(defineProps<Props>(), {
  title: 'Επιβεβαίωση',
  message: 'Είστε σίγουροι ότι θέλετε να συνεχίσετε;',
  confirmText: 'Επιβεβαίωση',
  cancelText: 'Ακύρωση',
  confirmColor: 'primary',
  icon: undefined,
  loading: false,
  persistent: false,
  maxWidth: 420,
})

const emit = defineEmits<{
  confirm: []
  cancel: []
}>()

const isOpen = defineModel<boolean>({ default: false })

function onCancel() {
  emit('cancel')
  isOpen.value = false
}

function onConfirm() {
  emit('confirm')
}
</script>

<template>
  <v-dialog
    v-model="isOpen"
    :max-width="maxWidth"
    :persistent="persistent || loading"
    @keydown.enter="!loading && onConfirm()"
  >
    <v-card :prepend-icon="icon" :title="title" rounded="lg">
      <v-divider />
      <v-card-text class="text-medium-emphasis">
        <slot>{{ message }}</slot>
      </v-card-text>

      <v-divider />

      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" :disabled="loading" @click="onCancel">
          {{ cancelText }}
        </v-btn>
        <v-btn :color="confirmColor" variant="flat" :loading="loading" @click="onConfirm">
          {{ confirmText }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
