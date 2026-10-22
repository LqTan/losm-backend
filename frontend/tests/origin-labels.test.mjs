import { test } from 'node:test';
import assert from 'node:assert/strict';
import { isCurrentUserOrigin, getOriginInitial } from '../lib/origin-labels.ts';

test('origin metadata takes precedence over localized names', () => {
  assert.equal(isCurrentUserOrigin({ isMe: false, name: 'Tôi' }), false);
  assert.equal(isCurrentUserOrigin({ isMe: false, name: 'Me' }), false);
  assert.equal(isCurrentUserOrigin({ isMe: true, name: 'Person 2' }), true);
  assert.equal(isCurrentUserOrigin({ name: 'Tôi' }), true);
  assert.equal(isCurrentUserOrigin({ name: 'Me' }), true);
  assert.equal(isCurrentUserOrigin({ name: 'You' }), true);
  assert.equal(isCurrentUserOrigin({ name: 'Person 1' }), false);
});
test('numbered avatars support both old Vietnamese and new English responses', () => {
  assert.equal(getOriginInitial('Người 12'), '12');
  assert.equal(getOriginInitial('Person 12'), '12');
  assert.equal(getOriginInitial('Nguyễn Văn An'), 'N');
});
