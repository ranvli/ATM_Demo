<script setup lang="ts">
import { onMounted, ref } from 'vue';

interface AccountSummary {
  id: string;
  name: string;
  balance: number;
  currency: string;
}

const accounts = ref<AccountSummary[]>([]);
const isLoading = ref(true);
const errorMessage = ref('');

async function loadAccounts(): Promise<void> {
  isLoading.value = true;
  errorMessage.value = '';

  try {
    const response = await fetch('/api/v1/accounts', {
      headers: { Accept: 'application/json' },
    });

    if (!response.ok) {
      throw new Error('The account request failed.');
    }

    accounts.value = (await response.json()) as AccountSummary[];
  } catch {
    errorMessage.value = 'Unable to load account balances.';
  } finally {
    isLoading.value = false;
  }
}

function formatBalance({ balance, currency }: AccountSummary): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency,
  }).format(balance);
}

onMounted(loadAccounts);
</script>

<template>
  <main class="terminal" aria-labelledby="terminal-title">
    <div class="terminal-screen">
      <h1 id="terminal-title" class="mb-4">ATM</h1>
      <section aria-labelledby="accounts-title">
        <h2 id="accounts-title" class="h5 mb-3">Account balances</h2>

        <p v-if="isLoading" class="mb-0" role="status">Loading balances…</p>
        <div v-else-if="errorMessage">
          <p role="alert">{{ errorMessage }}</p>
          <button class="btn btn-outline-light" type="button" @click="loadAccounts">
            Try again
          </button>
        </div>

        <dl v-else class="list-group list-group-flush mb-0">
          <div v-for="account in accounts" :key="account.id" class="list-group-item terminal-account">
            <dt>{{ account.name }}</dt>
            <dd class="mb-0">{{ formatBalance(account) }}</dd>
          </div>
        </dl>
      </section>
    </div>
  </main>
</template>
