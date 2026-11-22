import { computed, defineComponent, onMounted, ref } from 'vue';
import {
  AtmApiError,
  getOverview,
  submitOperation,
  type Operation,
  type Overview,
} from './atmApi';

const labels = {
  deposit: 'Deposit',
  withdrawal: 'Withdrawal',
  transfer: 'Transfer',
  history: 'History',
} as const;
type Screen = keyof typeof labels;

const screens: Screen[] = ['deposit', 'withdrawal', 'transfer', 'history'];
const keys = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '0'];
const partialAmountPattern = /^\d{0,13}(\.\d{0,2})?$/;
const completeAmountPattern = /^(?:\d{1,13}(?:\.\d{0,2})?|\.\d{1,2})$/;
const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });

export default defineComponent({
  name: 'AtmTerminal',
  setup() {
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

    const selectedAccount = computed(() =>
      overview.value?.accounts.find(account => account.id === accountId.value)
    );
    const otherAccount = computed(() =>
      overview.value?.accounts.find(account => account.id !== accountId.value)
    );
    const operationInProgress = computed(() => isSubmitting.value || awaitingConfirmation.value);

    const confirmButtonLabel = computed(() => {
      if (isSubmitting.value) return 'Processing…';
      return awaitingConfirmation.value ? 'Retry same operation' : 'Confirm';
    });

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
      if (partialAmountPattern.test(next)) {
        amount.value = next;
      }
    }

    function clearAmount(): void {
      amount.value = '';
    }

    function cancelReview(): void {
      pending.value = null;
    }

    function reviewOperation(): void {
      operationError.value = '';
      notice.value = '';
      const input = amount.value.trim().replace(',', '.');
      const value = Number(input);
      if (!completeAmountPattern.test(input) || value <= 0) {
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
          kind,
          operationId,
          amount: value,
          sourceAccountId: account.id,
          destinationAccountId: destination.id,
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

    return {
      labels,
      screens,
      keys,
      dateFormat,
      screen,
      overview,
      accountId,
      amount,
      pending,
      isLoading,
      isSubmitting,
      confirmButtonLabel,
      loadError,
      operationError,
      notice,
      selectedAccount,
      otherAccount,
      operationInProgress,
      refreshOverview,
      selectScreen,
      pressKey,
      clearAmount,
      reviewOperation,
      cancelReview,
      confirmOperation,
      money,
      describeAccounts,
    };
  },
});
