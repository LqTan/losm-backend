import { test } from 'node:test';
import assert from 'node:assert/strict';
import { getApiErrorMessage } from '../lib/api-error.ts';

test('API errors support all response shapes independently of language', () => {
  assert.equal(getApiErrorMessage({ message: 'Meeting title is required.' }, 'Bad Request'), 'Meeting title is required.');
  assert.equal(getApiErrorMessage({ error: 'The AI service is unavailable.' }, 'Error'), 'The AI service is unavailable.');
  assert.equal(getApiErrorMessage('Request must not be empty.', 'Error'), 'Request must not be empty.');
  assert.equal(getApiErrorMessage({ detail: 'Invalid input' }, 'Error'), 'Invalid input');
  assert.equal(getApiErrorMessage({ message: 'Thông báo cũ' }, 'Error'), 'Thông báo cũ');
  assert.equal(getApiErrorMessage({ message: null, error: 'Fallback error' }, 'Error'), 'Fallback error');
  assert.equal(getApiErrorMessage(null, 'Error'), 'Error');
});
