<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { AtmApiError, getOverview, submitOperation, type Operation, type Overview } from './atmApi';

const labels = {
  deposit: 'Deposit',
  withdrawal: 'Withdrawal',
  transfer: 'Transfer',
  history: 'History',
} as const;
type Screen = keyof typeof labels;

const screens: Screen[] = ['deposit', 'withdrawal', 'transfer', 'history'];
const keys = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '0'];
const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });
const screen = ref<Screen>('deposit');
const overview = ref<Overview | null>(null);
const accountId = ref('checking');
const amount = ref('');
const pending = ref<Operation | null>(null);
const isLoading = ref(true);
const isSubmitting = ref(false);
const awaitingConfirmation = ref(false);
const loadError = ref('');
const operationError = ref('');
const notice = ref('');

const selectedAccount = computed(() => overview.value?.accounts.find(account => account.id === accountId.value));
const otherAccount = computed(() => overview.value?.accounts.find(account => account.id !== accountId.value));
const operationInProgress = computed(() => isSubmitting.value || awaitingConfirmation.value);

async function refreshOverview(): Promise<void> {
  isLoading.value = true;
  loadError.value = '';
  try {
    overview.value = await getOverview();
  } catch {
    loadError.value = 'Unable to load balances and history.';
  } finally {
    isLoading.value = false;
  }
}

function selectScreen(next: Screen): void {
  screen.value = next;
  pending.value = null;
  amount.value = '';
  operationError.value = '';
  notice.value = '';
}

function pressKey(key: string): void {
  const next = (amount.value + key).replace(',', '.');
  if (/^\d{0,13}(\.\d{0,2})?$/.test(next)) {
    amount.value = next;
  }
}

function reviewOperation(): void {
  operationError.value = '';
  notice.value = '';
  const input = amount.value.trim().replace(',', '.');
  const value = Number(input);
  if (!/^(?:\d{1,13}(?:\.\d{0,2})?|\.\d{1,2})$/.test(input) || value <= 0) {
    operationError.value = 'Enter a positive amount with up to two decimal places.';
    return;
  }

  const account = selectedAccount.value;
  if (!account) {
    operationError.value = 'Select an account.';
    return;
  }

  const kind = screen.value;
  if (kind === 'history') return;

  const operationId = crypto.randomUUID();
  if (kind === 'transfer') {
    const destination = otherAccount.value;
    if (!destination) {
      operationError.value = 'A transfer requires a second account.';
      return;
    }

    pending.value = {
      kind, operationId, amount: value,
      sourceAccountId: account.id, destinationAccountId: destination.id,
    };
  } else if (kind === 'deposit') {
    pending.value = { kind, operationId, amount: value, destinationAccountId: account.id };
  } else {
    pending.value = { kind, operationId, amount: value, sourceAccountId: account.id };
  }
}

async function confirmOperation(): Promise<void> {
  const operation = pending.value;
  if (!operation || isSubmitting.value) return;

  isSubmitting.value = true;
  operationError.value = '';
  try {
    const transaction = await submitOperation(operation);
    pending.value = null;
    awaitingConfirmation.value = false;
    amount.value = '';
    notice.value = labels[operation.kind] + ' completed · ' + money(operation.amount, transaction.currency);
    await refreshOverview();
  } catch (error) {
    if (error instanceof AtmApiError && [400, 404, 409].includes(error.status)) {
      pending.value = null;
      awaitingConfirmation.value = false;
      operationError.value = error.message;
      await refreshOverview();
    } else {
      // Keep the same operation ID in case the server applied the movement before the response was lost.
      awaitingConfirmation.value = true;
      operationError.value = 'The result is unknown. Retry to check the same operation.';
    }
  } finally {
    isSubmitting.value = false;
  }
}

function money(amount: number, currency: string): string {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount);
}

function accountName(id: string): string {
  return overview.value?.accounts.find(account => account.id === id)?.name ?? id;
}

function describeAccounts(operation: Operation): string {
  if (operation.kind === 'transfer') {
    return accountName(operation.sourceAccountId) + ' → ' + accountName(operation.destinationAccountId);
  }
  if (operation.kind === 'deposit') {
    return 'To ' + accountName(operation.destinationAccountId);
  }
  return 'From ' + accountName(operation.sourceAccountId);
}

onMounted(refreshOverview);
</script>

<template>
  <main class="terminal" data-bs-theme="dark" aria-labelledby="terminal-title">
    <div class="terminal-screen">
      <h1 id="terminal-title" class="mb-4">ATM</h1>

      <p v-if="isLoading && !overview" class="mb-0" role="status">Loading balances…</p>
      <section v-if="overview" aria-labelledby="accounts-title">
        <h2 id="accounts-title" class="h5 mb-3">Account balances</h2>
        <dl class="list-group list-group-flush mb-3">
          <div v-for="account in overview.accounts" :key="account.id" class="list-group-item terminal-account">
            <dt>{{ account.name }}</dt>
            <dd class="mb-0">{{ money(account.balance, account.currency) }}</dd>
          </div>
        </dl>
      </section>

      <p v-if="notice" class="alert alert-success" role="status">{{ notice }}</p>
      <div v-if="loadError" class="mb-3">
        <p role="alert">{{ loadError }}</p>
        <button class="btn btn-outline-light" type="button" @click="refreshOverview">Try again</button>
      </div>

      <template v-if="overview && !loadError">
        <nav class="terminal-menu mb-4" aria-label="ATM operations">
          <button
            v-for="option in screens"
            :key="option"
            class="btn"
            :class="screen === option ? 'btn-light' : 'btn-outline-light'"
            type="button"
            :aria-pressed="screen === option"
            :disabled="operationInProgress || isLoading"
            @click="selectScreen(option)"
          >{{ labels[option] }}</button>
        </nav>

        <p v-if="operationError" class="alert alert-danger" role="alert">{{ operationError }}</p>

        <section v-if="screen === 'history'" aria-labelledby="history-title">
          <h2 id="history-title" class="h5">Transaction history</h2>
          <p v-if="overview.transactions.length === 0" class="mb-0">No movements yet.</p>
          <div v-else class="table-responsive">
            <table class="table table-sm table-dark mb-0">
              <thead>
                <tr>
                  <th scope="col">Operation</th>
                  <th scope="col">Accounts</th>
                  <th scope="col">Amount</th>
                  <th scope="col">Date</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="transaction in overview.transactions" :key="transaction.operation.operationId">
                  <td>{{ labels[transaction.operation.kind] }}</td>
                  <td>{{ describeAccounts(transaction.operation) }}</td>
                  <td>{{ money(transaction.operation.amount, transaction.currency) }}</td>
                  <td>{{ dateFormat.format(new Date(transaction.completedAt)) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <form v-else-if="pending" @submit.prevent="confirmOperation">
          <h2 class="h5">Confirm {{ labels[pending.kind].toLowerCase() }}</h2>
          <p>{{ describeAccounts(pending) }} · <strong>{{ money(pending.amount, selectedAccount?.currency ?? 'USD') }}</strong></p>
          <div class="d-flex flex-wrap gap-2">
            <button class="btn btn-success" type="submit" :disabled="isSubmitting">
              {{ isSubmitting ? 'Processing…' : awaitingConfirmation ? 'Retry same operation' : 'Confirm' }}
            </button>
            <button class="btn btn-outline-light" type="button" :disabled="operationInProgress" @click="pending = null">
              Back
            </button>
          </div>
        </form>

        <form v-else @submit.prevent="reviewOperation">
          <fieldset :disabled="isSubmitting || isLoading">
            <legend class="h5 mb-3">{{ labels[screen] }}</legend>
            <div class="terminal-entry">
              <div>
                <label class="form-label" for="account">{{ screen === 'transfer' ? 'From account' : 'Account' }}</label>
                <select id="account" v-model="accountId" class="form-select mb-2">
                  <option v-for="account in overview.accounts" :key="account.id" :value="account.id">
                    {{ account.name }}
                  </option>
                </select>
                <p v-if="screen === 'transfer'" class="mb-3">To {{ otherAccount?.name }}</p>

                <label class="form-label" for="amount">Amount ({{ selectedAccount?.currency }})</label>
                <input id="amount" v-model="amount" class="form-control mb-3" inputmode="decimal" maxlength="16" autocomplete="off">
                <button class="btn btn-success" type="submit">Review</button>
              </div>
              <div class="terminal-keypad" role="group" aria-label="Amount keypad">
                <button
                  v-for="key in keys"
                  :key="key"
                  class="btn btn-outline-light"
                  type="button"
                  :aria-label="key === '.' ? 'Decimal point' : undefined"
                  @click="pressKey(key)"
                >{{ key }}</button>
                <button class="btn btn-outline-light" type="button" @click="amount = ''">Clear</button>
              </div>
            </div>
          </fieldset>
        </form>
      </template>
    </div>
  </main>
</template>
