<script lang="ts" src="./AtmTerminal.ts"></script>

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
          <p>
            {{ describeAccounts(pending) }} ·
            <strong>{{ money(pending.amount, selectedAccount?.currency ?? 'USD') }}</strong>
          </p>
          <div class="d-flex flex-wrap gap-2">
            <button class="btn btn-success" type="submit" :disabled="isSubmitting">
              {{ confirmButtonLabel }}
            </button>
            <button class="btn btn-outline-light" type="button" :disabled="operationInProgress" @click="cancelReview">
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
                <input
                  id="amount"
                  v-model="amount"
                  class="form-control mb-3"
                  inputmode="decimal"
                  maxlength="16"
                  autocomplete="off"
                >
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
                <button class="btn btn-outline-light" type="button" @click="clearAmount">Clear</button>
              </div>
            </div>
          </fieldset>
        </form>
      </template>
    </div>
  </main>
</template>
