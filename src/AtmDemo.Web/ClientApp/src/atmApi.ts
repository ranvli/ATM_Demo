export interface Account {
  id: string;
  name: string;
  balance: number;
  currency: string;
}

export type Operation = { operationId: string; amount: number } & (
  | { kind: 'deposit'; sourceAccountId?: null; destinationAccountId: string }
  | { kind: 'withdrawal'; sourceAccountId: string; destinationAccountId?: null }
  | { kind: 'transfer'; sourceAccountId: string; destinationAccountId: string }
);

export interface Transaction {
  operation: Operation;
  currency: string;
  completedAt: string;
}

export interface Overview {
  accounts: Account[];
  transactions: Transaction[];
}

export class AtmApiError extends Error {
  constructor(readonly status: number, message: string) {
    super(message);
  }
}

async function request<T>(path: string, body?: object): Promise<T> {
  const headers = new Headers({ Accept: 'application/json, application/problem+json' });
  const options: RequestInit = { headers };

  if (body) {
    headers.set('Content-Type', 'application/json');
    options.method = 'POST';
    options.body = JSON.stringify(body);
  }

  const response = await fetch('/api/v1' + path, options);
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { title?: string } | null;
    throw new AtmApiError(response.status, problem?.title ?? 'The ATM request failed.');
  }

  return await response.json() as T;
}

export const getOverview = () => request<Overview>('/overview');

export function submitOperation(operation: Operation): Promise<Transaction> {
  const { operationId, amount } = operation;
  if (operation.kind === 'transfer') {
    return request('/transfers', {
      operationId,
      amount,
      sourceAccountId: operation.sourceAccountId,
      destinationAccountId: operation.destinationAccountId,
    });
  }

  const isDeposit = operation.kind === 'deposit';
  const accountId = isDeposit ? operation.destinationAccountId : operation.sourceAccountId;
  const action = isDeposit ? 'deposits' : 'withdrawals';
  return request('/accounts/' + encodeURIComponent(accountId) + '/' + action, { operationId, amount });
}
